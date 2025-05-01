using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

class UniversalByteRemover
{
    static void Main(string[] args)
    {
        Console.WriteLine("请输入一个有效的路径: ");
        string directoryPath;
        string? inputPath = Console.ReadLine();
        while (string.IsNullOrEmpty(inputPath) || !Directory.Exists(inputPath))
        {
            Console.WriteLine("输入的路径无效，请重新输入。");
            inputPath = Console.ReadLine();
        }
        directoryPath = inputPath;

        // 提示用户是否备份数据
        Console.Write("接下来的操作将会破坏数据，极为危险，是否要备份数据？（输入 '是' 或 '否'）: ");
        string backupChoice = Console.ReadLine()?.ToLower() ?? "";

        if (backupChoice == "是")
        {
            string? parentDirectory = Path.GetDirectoryName(directoryPath);
            if (parentDirectory == null)
            {
                Console.WriteLine("无法获取父目录路径，操作终止。");
                return;
            }
            string backupDir = Path.Combine(parentDirectory, $"{Path.GetFileName(directoryPath)}备份");
            CopyDirectory(directoryPath, backupDir);
            Console.WriteLine($"已成功备份文件夹 {directoryPath} 到 {backupDir}");
        }
        else if (backupChoice != "否")
        {
            Console.WriteLine("输入无效，默认不备份，继续操作。");
        }

        Console.WriteLine("请选择模式（1：删除指定字节序列前面的所有字节，2：删除指定字节序列及其后面的所有字节，" +
                         "3：删除指定地址前面的所有字节，4：删除指定地址后的所有字节，" +
                         "5：删除起始地址前面和结束地址后面的所有字节，6：删除文件尾部的重复字节，" +
                         "7：删除文件尾部指定数量字节，8：删除文件开头指定数量字节）: ");
        string? mode = Console.ReadLine();

        Console.WriteLine("请输入指定的字节序列（以十六进制形式，例如：'50554946' 或 '50 55 49 46'）: ");
        string byteSequenceInput = Console.ReadLine()?.Replace(" ", "") ?? "";
        byte[] byteSequence = StringToByteArray(byteSequenceInput);

        string newExtension = GetNewExtension();

        switch (mode)
        {
            case "1":
                Console.WriteLine("请选择从第几个字节序列前面删除多余字节（从 1 开始）: ");
                string? seqNumInput = Console.ReadLine();
                if (!int.TryParse(seqNumInput, out int seqNum))
                {
                    Console.WriteLine("输入的序号无效，默认使用 1");
                    seqNum = 1;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveBeforeSequence(filePath, byteSequence, newExtension, seqNum));
                break;
            case "2":
                ProcessFiles(directoryPath, filePath =>
                    RemoveByteSequenceAndAfter(filePath, byteSequence, newExtension));
                break;
            case "3":
                Console.WriteLine("请输入指定地址（例如：0x00006F20）: ");
                string? addressInput = Console.ReadLine();
                if (!int.TryParse(addressInput, NumberStyles.HexNumber, null, out int addressForMode3))
                {
                    Console.WriteLine("输入的地址无效，操作终止");
                    return;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveBeforeAddress(filePath, addressForMode3, newExtension));
                break;
            case "4":
                Console.WriteLine("请输入指定地址（例如：0x00006F20）: ");
                addressInput = Console.ReadLine();
                if (!int.TryParse(addressInput, NumberStyles.HexNumber, null, out int addressForMode4))
                {
                    Console.WriteLine("输入的地址无效，操作终止");
                    return;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveAfterAddress(filePath, addressForMode4, newExtension));
                break;
            case "5":
                Console.WriteLine("请输入起始地址（例如：0x00006F20）: ");
                string? startAddressInput = Console.ReadLine();
                if (!int.TryParse(startAddressInput, NumberStyles.HexNumber, null, out int startAddress))
                {
                    Console.WriteLine("输入的起始地址无效，操作终止");
                    return;
                }
                Console.WriteLine("请输入结束地址（例如：0x00007F20）: ");
                string? endAddressInput = Console.ReadLine();
                if (!int.TryParse(endAddressInput, NumberStyles.HexNumber, null, out int endAddress))
                {
                    Console.WriteLine("输入的结束地址无效，操作终止");
                    return;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveOutsideAddresses(filePath, startAddress, endAddress, newExtension));
                break;
            case "6":
                Console.WriteLine("请输入要检查的字节（以十六进制形式，例如：'30'）: ");
                string? byteToCheckHex = Console.ReadLine();
                try
                {
                    byte byteToCheck = Convert.ToByte(byteToCheckHex, 16);
                    ProcessFiles(directoryPath, filePath =>
                        RemoveTrailingRepeatedBytes(filePath, byteToCheck));
                }
                catch (FormatException)
                {
                    Console.WriteLine("输入的十六进制字节无效，操作终止");
                    return;
                }
                break;
            case "7":
                Console.WriteLine("请输入要删除的文件尾部字节数量: ");
                string? numBytesInput = Console.ReadLine();
                if (!int.TryParse(numBytesInput, out int numBytesForMode7))
                {
                    Console.WriteLine("输入的字节数量无效，操作终止");
                    return;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveFixedTrailingBytes(filePath, numBytesForMode7, newExtension));
                break;
            case "8":
                Console.WriteLine("请输入要删除的文件开头字节数量: ");
                numBytesInput = Console.ReadLine();
                if (!int.TryParse(numBytesInput, out int numBytesForMode8))
                {
                    Console.WriteLine("输入的字节数量无效，操作终止");
                    return;
                }
                ProcessFiles(directoryPath, filePath =>
                    RemoveFixedStartingBytes(filePath, numBytesForMode8, newExtension));
                break;
            default:
                Console.WriteLine("无效的模式选择");
                break;
        }
    }

    static string GetNewExtension()
    {
        Console.WriteLine("请输入新的文件扩展名（例如：'acb'）: ");
        string? newExtensionInput = Console.ReadLine();
        return string.IsNullOrEmpty(newExtensionInput) ? "" : newExtensionInput;
    }

    static List<int> FindAllSequences(byte[] content, byte[] sequence)
    {
        List<int> indices = new List<int>();
        for (int i = 0; i <= content.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (content[i + j] != sequence[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                indices.Add(i);
                i += sequence.Length - 1; // Skip ahead to avoid overlapping matches
            }
        }
        return indices;
    }

    static void ProcessFiles(string directoryPath, Action<string> processAction)
    {
        int totalFilesProcessed = 0;
        int totalFiles = 0;

        foreach (string filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(".py") || filePath.ToLower().Contains("disabled"))
            {
                continue;
            }

            totalFiles++;
            try
            {
                processAction(filePath);
                totalFilesProcessed++;
                double progress = (double)totalFilesProcessed / totalFiles * 100;
                Console.Write($"\r处理进度: {progress:F2}%");
            }
            catch (Exception e)
            {
                Console.WriteLine($"处理文件 {filePath} 时出错: {e.Message}");
            }
        }

        Console.WriteLine($"\n处理了 {totalFilesProcessed} 个文件.");
    }

    static void RemoveByteSequenceAndAfter(string filePath, byte[] sequence, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        int index = Array.IndexOf(content, sequence[0]);

        while (index != -1 && index <= content.Length - sequence.Length)
        {
            bool match = true;
            for (int i = 0; i < sequence.Length; i++)
            {
                if (content[index + i] != sequence[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                byte[] newContent = new byte[index];
                Array.Copy(content, newContent, index);
                File.WriteAllBytes(filePath, newContent);

                string directoryName = Path.GetDirectoryName(filePath) ?? "";
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
                string newFilePath = Path.Combine(
                    directoryName,
                    $"{fileNameWithoutExtension}.{newExtension}");

                if (!File.Exists(newFilePath))
                {
                    File.Move(filePath, newFilePath);
                    Console.WriteLine($"已重命名文件为: {newFilePath}");
                }
                else
                {
                    Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
                }
                return;
            }

            index = Array.IndexOf(content, sequence[0], index + 1);
        }

        Console.WriteLine($"文件 {filePath} 中未找到指定字节序列，未进行操作");
    }

    static void RemoveBeforeSequence(string filePath, byte[] sequence, string newExtension, int seqNum)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        List<int> indices = FindAllSequences(content, sequence);

        if (indices.Count >= seqNum)
        {
            int sequenceIndex = indices[seqNum - 1];
            byte[] newContent = new byte[content.Length - sequenceIndex];
            Array.Copy(content, sequenceIndex, newContent, 0, newContent.Length);

            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
    }

    static void RemoveBeforeAddress(string filePath, int address, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (address < content.Length)
        {
            byte[] newContent = new byte[content.Length - address];
            Array.Copy(content, address, newContent, 0, newContent.Length);
            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
    }

    static void RemoveAfterAddress(string filePath, int address, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (address < content.Length)
        {
            byte[] newContent = new byte[address];
            Array.Copy(content, newContent, address);
            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
    }

    static void RemoveOutsideAddresses(string filePath, int startAddress, int endAddress, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (startAddress < content.Length && endAddress < content.Length && startAddress < endAddress)
        {
            byte[] newContent = new byte[endAddress - startAddress];
            Array.Copy(content, startAddress, newContent, 0, newContent.Length);
            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
    }

    static void RemoveTrailingRepeatedBytes(string filePath, byte byteToCheck)
    {
        byte[] content = File.ReadAllBytes(filePath);
        int i = content.Length - 1;
        while (i >= 0 && content[i] == byteToCheck)
        {
            i--;
        }

        byte[] newContent = new byte[i + 1];
        Array.Copy(content, newContent, i + 1);
        File.WriteAllBytes(filePath, newContent);
        Console.WriteLine($"已成功删除文件 {filePath} 尾部的指定重复字节");
    }

    static void RemoveFixedTrailingBytes(string filePath, int numBytes, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (numBytes < content.Length)
        {
            byte[] newContent = new byte[content.Length - numBytes];
            Array.Copy(content, newContent, content.Length - numBytes);
            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
        else
        {
            Console.WriteLine($"要删除的字节数大于文件长度，无法操作文件 {filePath}");
        }
    }

    static void RemoveFixedStartingBytes(string filePath, int numBytes, string newExtension)
    {
        if (string.IsNullOrEmpty(newExtension))
        {
            newExtension = Path.GetExtension(filePath).TrimStart('.');
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (numBytes < content.Length)
        {
            byte[] newContent = new byte[content.Length - numBytes];
            Array.Copy(content, numBytes, newContent, 0, content.Length - numBytes);
            File.WriteAllBytes(filePath, newContent);

            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? "";
            string newFilePath = Path.Combine(
                directoryName,
                $"{fileNameWithoutExtension}.{newExtension}");

            if (!File.Exists(newFilePath))
            {
                File.Move(filePath, newFilePath);
                Console.WriteLine($"已重命名文件为: {newFilePath}");
            }
            else
            {
                Console.WriteLine($"文件 {newFilePath} 已存在，未重命名。");
            }
        }
        else
        {
            Console.WriteLine($"要删除的字节数大于文件长度，无法操作文件 {filePath}");
        }
    }

    static byte[] StringToByteArray(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("十六进制字符串长度必须是偶数");
        }

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string[] files = Directory.GetFiles(sourceDir);
        foreach (string file in files)
        {
            string targetFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, targetFile, true);
        }

        string[] directories = Directory.GetDirectories(sourceDir);
        foreach (string dir in directories)
        {
            string targetSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, targetSubDir);
        }
    }
}