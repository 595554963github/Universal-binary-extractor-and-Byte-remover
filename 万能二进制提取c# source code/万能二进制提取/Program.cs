using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class FileExtractor
{
    static void Main()
    {
        // 获取要处理的文件夹路径
        Console.WriteLine("请输入要处理的文件夹路径: ");
        string? directoryPath = Console.ReadLine();
        if (!Directory.Exists(directoryPath))
        {
            Console.WriteLine($"错误: {directoryPath} 不是一个有效的目录。");
            return;
        }

        // 获取提取模式
        Console.WriteLine("请选择提取模式（1:正常提取，2:提取指定地址前的内容，3:提取指定地址后的内容）: ");
        string? extractMode = Console.ReadLine();

        switch (extractMode)
        {
            case "2":
                // 提取指定地址前的内容
                Console.WriteLine("请输入指定地址（例如: 0x00006F20）: ");
                string? targetAddress = Console.ReadLine();
                var (startSequenceBytes, endSequenceBytes, useRepeatMethod, minRepeatCount, outputFormat) = GetExtractionParameters();
                foreach (var filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    Console.WriteLine($"Processing file: {filePath}");
                    ExtractBeforeAddress(filePath, targetAddress, startSequenceBytes, endSequenceBytes, outputFormat, minRepeatCount);
                }
                break;
            case "3":
                // 提取指定地址后的内容
                Console.WriteLine("请输入指定地址（例如: 0x00006F20）: ");
                targetAddress = Console.ReadLine();
                (startSequenceBytes, endSequenceBytes, useRepeatMethod, minRepeatCount, outputFormat) = GetExtractionParameters();
                foreach (var filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    Console.WriteLine($"Processing file: {filePath}");
                    ExtractAfterAddress(filePath, targetAddress, startSequenceBytes, endSequenceBytes, outputFormat, minRepeatCount);
                }
                break;
            case "1":
                // 正常提取
                (startSequenceBytes, endSequenceBytes, useRepeatMethod, minRepeatCount, outputFormat) = GetExtractionParameters();
                foreach (var filePath in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    Console.WriteLine($"Processing file: {filePath}");
                    if (useRepeatMethod)
                    {
                        ExtractContentRepeat(filePath, startSequenceBytes, endSequenceBytes, outputFormat, minRepeatCount);
                    }
                    else
                    {
                        ExtractContentNormal(filePath, startSequenceBytes, endSequenceBytes, outputFormat);
                    }
                }
                break;
            default:
                Console.WriteLine("无效的提取模式选择，请重新运行脚本并正确选择。");
                break;
        }
    }

    // 正常提取内容
    static void ExtractContentNormal(string filePath, byte[] startSequenceBytes, byte[]? endSequenceBytes, string outputFormat)
    {
        try
        {
            byte[] content = File.ReadAllBytes(filePath);
            int count = 0;
            int startIndex = 0;
            var notes = new List<string>();

            while (startIndex < content.Length)
            {
                startIndex = FindSequence(content, startSequenceBytes, startIndex);
                if (startIndex == -1)
                {
                    Console.WriteLine($"No more start sequences found in {filePath}");
                    break;
                }

                int endIndex = GetEndIndex(content, startIndex, endSequenceBytes, startSequenceBytes);
                byte[] extractedData = new byte[endIndex - startIndex];
                Array.Copy(content, startIndex, extractedData, 0, extractedData.Length);

                string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName == null)
                {
                    Console.WriteLine($"无法获取文件 {filePath} 的目录信息，跳过该文件。");
                    return;
                }
                string newFilePath = Path.Combine(directoryName, newFilename);
                File.WriteAllBytes(newFilePath, extractedData);
                Console.WriteLine($"Extracted content saved as: {newFilePath}");

                notes.Add($"File: {newFilePath}, Start Address: {startIndex}, End Address: {endIndex}");
                count++;
                startIndex = endIndex;
            }

            SaveNotes(filePath, notes);
        }
        catch (IOException e)
        {
            Console.WriteLine($"无法读取文件 {filePath}，错误信息：{e}");
        }
    }

    // 按重复规则提取内容
    static void ExtractContentRepeat(string filePath, byte[] startSequenceBytes, byte[]? endSequenceBytes, string outputFormat, int minRepeatCount)
    {
        try
        {
            byte[] content = File.ReadAllBytes(filePath);
            int count = 0;
            int startIndex = 0;
            var notes = new List<string>();

            while (startIndex < content.Length)
            {
                startIndex = FindSequence(content, startSequenceBytes, startIndex);
                if (startIndex == -1)
                {
                    Console.WriteLine($"No more start sequences found in {filePath}");
                    break;
                }

                int endIndex = FindEndIndex(content, startIndex, endSequenceBytes, minRepeatCount, startSequenceBytes);
                byte[] extractedData = new byte[endIndex - startIndex];
                Array.Copy(content, startIndex, extractedData, 0, extractedData.Length);

                string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName == null)
                {
                    Console.WriteLine($"无法获取文件 {filePath} 的目录信息，跳过该文件。");
                    return;
                }
                string newFilePath = Path.Combine(directoryName, newFilename);
                File.WriteAllBytes(newFilePath, extractedData);
                Console.WriteLine($"Extracted content saved as: {newFilePath}");

                notes.Add($"File: {newFilePath}, Start Address: {startIndex}, End Address: {endIndex}");
                count++;
                startIndex = endIndex;
            }

            SaveNotes(filePath, notes);
        }
        catch (IOException e)
        {
            Console.WriteLine($"无法读取文件 {filePath}，错误信息：{e}");
        }
    }

    // 提取指定地址前的内容
    static void ExtractBeforeAddress(string filePath, string? targetAddress, byte[] startSequenceBytes, byte[]? endSequenceBytes, string outputFormat, int minRepeatCount)
    {
        try
        {
            byte[] content = File.ReadAllBytes(filePath);
            int targetIndex = Convert.ToInt32(targetAddress?.Replace("0x", ""), 16);
            if (targetIndex > content.Length)
            {
                Console.WriteLine($"指定地址 {targetAddress} 超出文件范围，无法提取。");
                return;
            }

            int count = 0;
            int startIndex = 0;
            var notes = new List<string>();

            while (startIndex < content.Length && startIndex < targetIndex)
            {
                startIndex = FindSequence(content, startSequenceBytes, startIndex);
                if (startIndex == -1)
                {
                    Console.WriteLine($"No more start sequences found in {filePath} before the target address");
                    break;
                }

                int endIndex = GetEndIndex(content, startIndex, endSequenceBytes, startSequenceBytes);
                if (endIndex > targetIndex)
                {
                    endIndex = targetIndex;
                }
                byte[] extractedData = new byte[endIndex - startIndex];
                Array.Copy(content, startIndex, extractedData, 0, extractedData.Length);

                string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName == null)
                {
                    Console.WriteLine($"无法获取文件 {filePath} 的目录信息，跳过该文件。");
                    return;
                }
                string newFilePath = Path.Combine(directoryName, newFilename);
                File.WriteAllBytes(newFilePath, extractedData);
                Console.WriteLine($"Extracted content saved as: {newFilePath}");

                notes.Add($"File: {newFilePath}, Start Address: {startIndex}, End Address: {endIndex}");
                count++;
                startIndex = endIndex;
            }

            SaveNotes(filePath, notes);
        }
        catch (IOException e)
        {
            Console.WriteLine($"无法读取文件 {filePath}，错误信息：{e}");
        }
    }

    // 提取指定地址后的内容
    static void ExtractAfterAddress(string filePath, string? targetAddress, byte[] startSequenceBytes, byte[]? endSequenceBytes, string outputFormat, int minRepeatCount)
    {
        try
        {
            byte[] content = File.ReadAllBytes(filePath);
            int targetIndex = Convert.ToInt32(targetAddress?.Replace("0x", ""), 16);
            if (targetIndex > content.Length)
            {
                Console.WriteLine($"指定地址 {targetAddress} 超出文件范围，无法提取。");
                return;
            }

            int count = 0;
            int startIndex = targetIndex;
            var notes = new List<string>();

            while (startIndex < content.Length)
            {
                startIndex = FindSequence(content, startSequenceBytes, startIndex);
                if (startIndex == -1)
                {
                    Console.WriteLine($"No more start sequences found in {filePath} after the target address");
                    break;
                }

                int endIndex = GetEndIndex(content, startIndex, endSequenceBytes, startSequenceBytes);
                byte[] extractedData = new byte[endIndex - startIndex];
                Array.Copy(content, startIndex, extractedData, 0, extractedData.Length);

                string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName == null)
                {
                    Console.WriteLine($"无法获取文件 {filePath} 的目录信息，跳过该文件。");
                    return;
                }
                string newFilePath = Path.Combine(directoryName, newFilename);
                File.WriteAllBytes(newFilePath, extractedData);
                Console.WriteLine($"Extracted content saved as: {newFilePath}");

                notes.Add($"File: {newFilePath}, Start Address: {startIndex}, End Address: {endIndex}");
                count++;
                startIndex = endIndex;
            }

            SaveNotes(filePath, notes);
        }
        catch (IOException e)
        {
            Console.WriteLine($"无法读取文件 {filePath}，错误信息：{e}");
        }
    }

    // 查找序列在内容中的起始位置
    static int FindSequence(byte[] content, byte[] sequence, int startIndex)
    {
        for (int i = startIndex; i <= content.Length - sequence.Length; i++)
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
                return i;
            }
        }
        return -1;
    }

    // 获取结束索引
    static int GetEndIndex(byte[] content, int startIndex, byte[]? endSequenceBytes, byte[] startSequenceBytes)
    {
        if (endSequenceBytes != null && endSequenceBytes.Length > 0)
        {
            int endIndex = FindSequence(content, endSequenceBytes, startIndex + startSequenceBytes.Length);
            return endIndex == -1 ? content.Length : endIndex + endSequenceBytes.Length;
        }
        else
        {
            int nextStartIndex = FindSequence(content, startSequenceBytes, startIndex + startSequenceBytes.Length);
            return nextStartIndex == -1 ? content.Length : nextStartIndex;
        }
    }

    // 按重复规则查找结束索引
    static int FindEndIndex(byte[] content, int startIndex, byte[]? endSequenceBytes, int minRepeatCount, byte[] startSequenceBytes)
    {
        if (endSequenceBytes == null || endSequenceBytes.Length == 0)
        {
            int nextStartIndex = FindSequence(content, startSequenceBytes, startIndex + 1);
            return nextStartIndex == -1 ? content.Length : nextStartIndex;
        }
        else
        {
            if (minRepeatCount == 0)
            {
                int endIndex = FindSequence(content, endSequenceBytes, startIndex + 1);
                return endIndex == -1 ? content.Length : endIndex + endSequenceBytes.Length;
            }
            else
            {
                byte byteValue = endSequenceBytes[0];
                int repeatCount = 0;
                int currentIndex = startIndex + 1;
                while (currentIndex < content.Length)
                {
                    if (content[currentIndex] == byteValue)
                    {
                        repeatCount++;
                        if (repeatCount >= minRepeatCount && (minRepeatCount == 0 || content[currentIndex + 1] != byteValue))
                        {
                            return currentIndex + 1;
                        }
                    }
                    else
                    {
                        repeatCount = 0;
                    }
                    currentIndex++;
                }
                return content.Length;
            }
        }
    }

    // 保存提取信息到笔记文件
    static void SaveNotes(string filePath, List<string> notes)
    {
        string notesFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_notes.txt";
        string? directoryName = Path.GetDirectoryName(filePath);
        if (directoryName == null)
        {
            Console.WriteLine($"无法获取文件 {filePath} 的目录信息，无法保存笔记。");
            return;
        }
        string notesFilePath = Path.Combine(directoryName, notesFilename);
        File.WriteAllLines(notesFilePath, notes);
        Console.WriteLine($"Notes saved as: {notesFilePath}");
    }

    // 获取提取参数
    static (byte[] startSequenceBytes, byte[]? endSequenceBytes, bool useRepeatMethod, int minRepeatCount, string outputFormat) GetExtractionParameters()
    {
        Console.WriteLine("请输入起始序列的字节值，以空格分隔（也可输入类似00*16）: ");
        string? startSequenceInput = Console.ReadLine();
        byte[] startSequenceBytes = ParseSequence(startSequenceInput);

        Console.WriteLine("请输入结束序列字节值（以空格分割，使用*表示重复，如00*4，直接回车跳过）: ");
        string? endSequenceInput = Console.ReadLine();
        bool useRepeatMethod = false;
        int minRepeatCount = 0;
        byte[]? endSequenceBytes = null;
        if (!string.IsNullOrEmpty(endSequenceInput))
        {
            endSequenceBytes = ParseSequence(endSequenceInput);
            if (!endSequenceInput.Contains('*') && endSequenceBytes.Length == 1)
            {
                Console.WriteLine("请输入最小重复字节数量作为结束条件: ");
                if (int.TryParse(Console.ReadLine(), out minRepeatCount))
                {
                    useRepeatMethod = true;
                }
            }
        }

        Console.WriteLine("请输入输出文件格式 (例如: bin): ");
        string outputFormat = Console.ReadLine() ?? "bin";

        return (startSequenceBytes, endSequenceBytes, useRepeatMethod, minRepeatCount, outputFormat);
    }

    // 解析序列输入
    static byte[] ParseSequence(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return Array.Empty<byte>();
        }
        var result = new List<byte>();
        string[] parts = input.Split(' ');
        foreach (string part in parts)
        {
            if (part.Contains('*'))
            {
                string[] subParts = part.Split('*');
                byte byteValue = Convert.ToByte(subParts[0], 16);
                int repeatCount = int.Parse(subParts[1]);
                result.AddRange(Enumerable.Repeat(byteValue, repeatCount));
            }
            else
            {
                result.Add(Convert.ToByte(part, 16));
            }
        }
        return result.ToArray();
    }
}