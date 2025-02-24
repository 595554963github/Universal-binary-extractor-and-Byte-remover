using System;
using System.Collections.Generic;
using System.IO;

class FileExtractor
{
    static byte[] ParseStartSequence(string startSequenceInput)
    {
        // 检查输入是否为 null 或空字符串
        if (string.IsNullOrEmpty(startSequenceInput))
        {
            return Array.Empty<byte>();
        }

        if (startSequenceInput.Contains('*'))
        {
            string[] parts = startSequenceInput.Split('*');
            byte byteValue = Convert.ToByte(parts[0].Replace(" ", ""), 16);
            int repeatCount = int.Parse(parts[1]);
            byte[] result = new byte[repeatCount];
            for (int i = 0; i < repeatCount; i++)
            {
                result[i] = byteValue;
            }
            return result;
        }
        else
        {
            return StringToByteArray(startSequenceInput.Replace(" ", ""));
        }
    }

    static byte[] ParseEndSequence(string endSequenceInput)
    {
        // 检查输入是否为 null 或空字符串
        if (string.IsNullOrEmpty(endSequenceInput))
        {
            return Array.Empty<byte>();
        }

        string[] parts = endSequenceInput.Split(' ');
        List<byte> result = new List<byte>();
        foreach (string part in parts)
        {
            if (part.Contains('*'))
            {
                string[] subParts = part.Split('*');
                byte byteValue = Convert.ToByte(subParts[0].Replace(" ", ""), 16);
                int repeatCount = int.Parse(subParts[1]);
                for (int i = 0; i < repeatCount; i++)
                {
                    result.Add(byteValue);
                }
            }
            else
            {
                result.Add(Convert.ToByte(part.Replace(" ", ""), 16));
            }
        }
        return result.ToArray();
    }

    static int FindEndIndex(byte[] content, int startIndex, byte[]? endSequence, int minRepeatCount, byte[] startSequenceBytes)
    {
        if (endSequence == null || endSequence.Length == 0)
        {
            int nextStartIndex = IndexOfSequence(content, startSequenceBytes, startIndex + 1);
            return nextStartIndex == -1 ? content.Length : nextStartIndex;
        }
        else
        {
            if (minRepeatCount == 0)
            {
                int endIndex = IndexOfSequence(content, endSequence, startIndex + 1);
                return endIndex == -1 ? content.Length : endIndex + endSequence.Length;
            }
            else
            {
                byte byteValue = endSequence[0];
                int repeatCount = 0;
                int currentIndex = startIndex + 1;
                while (currentIndex < content.Length)
                {
                    if (content[currentIndex] == byteValue)
                    {
                        repeatCount++;
                        if (repeatCount >= minRepeatCount && (minRepeatCount == 0 || (currentIndex + 1 < content.Length && content[currentIndex + 1] != byteValue)))
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

    static void ExtractContent(string filePath, byte[] startSequenceBytes, byte[]? endSequence = null, string outputFormat = "bin",
                               string extractMode = "all", string? startAddress = null, string? endAddress = null, int minRepeatCount = 0)
    {
        // 检查输出格式是否为 null
        outputFormat = outputFormat ?? "bin";

        try
        {
            byte[] content = File.ReadAllBytes(filePath);
            int startRange = 0;
            int endRange = content.Length;

            if (startAddress != null && endAddress != null)
            {
                int startIndex = Convert.ToInt32(startAddress.Replace("0x", ""), 16);
                int endIndex = Convert.ToInt32(endAddress.Replace("0x", ""), 16);
                if (startIndex > content.Length || endIndex > content.Length || startIndex > endIndex)
                {
                    Console.WriteLine($"指定地址范围 {startAddress}-{endAddress} 无效，无法提取。");
                    return;
                }
                startRange = startIndex;
                endRange = endIndex;
            }
            else if (startAddress != null)
            {
                int targetIndex = Convert.ToInt32(startAddress.Replace("0x", ""), 16);
                if (targetIndex > content.Length)
                {
                    Console.WriteLine($"指定地址 {startAddress} 超出文件范围，无法提取。");
                    return;
                }
                if (extractMode == "before")
                {
                    startRange = 0;
                    endRange = targetIndex;
                }
                else if (extractMode == "after")
                {
                    startRange = targetIndex;
                    endRange = content.Length;
                }
                else
                {
                    Console.WriteLine("无效的提取模式参数");
                    return;
                }
            }

            int count = 0;
            int startIndexInContent = startRange;
            List<string> notes = new List<string>();
            while (startIndexInContent < endRange)
            {
                startIndexInContent = IndexOfSequence(content, startSequenceBytes, startIndexInContent);
                if (startIndexInContent == -1)
                {
                    Console.WriteLine($"No more start sequences found in {filePath}");
                    break;
                }

                int endIndexInContent = FindEndIndex(content, startIndexInContent, endSequence, minRepeatCount, startSequenceBytes);
                endIndexInContent = Math.Min(endIndexInContent, endRange);

                byte[] extractedData = new byte[endIndexInContent - startIndexInContent];
                Array.Copy(content, startIndexInContent, extractedData, 0, extractedData.Length);

                string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                string? directoryName = Path.GetDirectoryName(filePath);
                if (directoryName == null)
                {
                    Console.WriteLine($"无法获取文件 {filePath} 的目录信息，跳过该文件。");
                    continue;
                }
                string newFilePath = Path.Combine(directoryName, newFilename);
                try
                {
                    File.WriteAllBytes(newFilePath, extractedData);
                }
                catch (IOException e)
                {
                    Console.WriteLine($"无法写入文件 {newFilePath}，错误信息：{e}");
                    continue;
                }
                Console.WriteLine($"Extracted content saved as: {newFilePath}");

                notes.Add($"File: {newFilePath}, Start Address: {startIndexInContent}, End Address: {endIndexInContent}");
                count++;
                startIndexInContent = endIndexInContent;
            }

            SaveNotes(filePath, notes);
        }
        catch (IOException e)
        {
            Console.WriteLine($"无法读取文件 {filePath}，错误信息：{e}");
        }
    }

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

    static int IndexOfSequence(byte[] source, byte[] sequence, int startIndex)
    {
        for (int i = startIndex; i <= source.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (source[i + j] != sequence[j])
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

    static byte[] StringToByteArray(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    static void Main()
    {
        Console.WriteLine("请输入要处理的文件夹路径: ");
        string? directoryPath = Console.ReadLine();
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            Console.WriteLine($"错误: {directoryPath} 不是一个有效的目录。");
            return;
        }

        Console.WriteLine("请选择提取模式（1:正常提取，2:提取指定地址前的内容，3:提取指定地址后的内容，4:从两个地址之间提取数据）: ");
        string? extractModeInput = Console.ReadLine();
        string? startAddress = null;
        string? endAddress = null;
        string extractMode = "";

        if (extractModeInput == "2" || extractModeInput == "3")
        {
            Console.WriteLine("请输入指定地址（例如: 0x00006F20）: ");
            startAddress = Console.ReadLine();
            if (extractModeInput == "2")
            {
                extractMode = "before";
            }
            else
            {
                extractMode = "after";
            }
        }
        else if (extractModeInput == "4")
        {
            Console.WriteLine("请输入起始地址（例如: 0x00006F20）: ");
            startAddress = Console.ReadLine();
            Console.WriteLine("请输入结束地址（例如: 0x00007F20）: ");
            endAddress = Console.ReadLine();
            extractMode = "between";
        }
        else
        {
            extractMode = "all";
        }

        Console.WriteLine("请输入起始序列的字节值，以空格分隔（也可输入类似00*16）: ");
        string? startSequenceInput = Console.ReadLine();
        if (startSequenceInput == null)
        {
            Console.WriteLine("起始序列输入为空，请重新输入。");
            return;
        }
        byte[] startSequenceBytes = ParseStartSequence(startSequenceInput);

        Console.WriteLine("请输入结束序列字节值（以空格分割，使用*表示重复，如00*4，直接回车跳过）: ");
        string? endSequenceInput = Console.ReadLine();
        int minRepeatCount = 0;
        byte[]? endSequenceBytes = null;
        if (!string.IsNullOrEmpty(endSequenceInput))
        {
            endSequenceBytes = ParseEndSequence(endSequenceInput);
            if (!endSequenceInput.Contains('*') && endSequenceBytes.Length == 1)
            {
                Console.WriteLine("请输入最小重复字节数量作为结束条件: ");
                if (int.TryParse(Console.ReadLine(), out minRepeatCount))
                {

                }
            }
        }

        Console.WriteLine("请输入输出文件格式 (例如: bin): ");
        string? outputFormat = Console.ReadLine();
        outputFormat = outputFormat ?? "bin";

        string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            Console.WriteLine($"Processing file: {file}");
            ExtractContent(file, startSequenceBytes, endSequenceBytes, outputFormat, extractMode, startAddress, endAddress, minRepeatCount);
        }
    }
}