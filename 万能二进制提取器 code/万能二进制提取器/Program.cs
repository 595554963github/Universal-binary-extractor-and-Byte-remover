using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

class FileExtractor
{
    static byte[] ParseStartSequence(string startSequenceInput)
    {
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

    static long FindEndIndex(MemoryMappedViewAccessor accessor, long startIndex, byte[]? endSequence, int minRepeatCount, byte[] startSequenceBytes, long fileSize)
    {
        if (endSequence == null || endSequence.Length == 0)
        {
            long nextStartIndex = IndexOfSequence(accessor, startSequenceBytes, startIndex + 1, fileSize);
            return nextStartIndex == -1 ? fileSize : nextStartIndex;
        }
        else
        {
            if (minRepeatCount == 0)
            {
                long endIndex = IndexOfSequence(accessor, endSequence, startIndex + 1, fileSize);
                return endIndex == -1 ? fileSize : endIndex + endSequence.Length;
            }
            else
            {
                byte byteValue = endSequence[0];
                int repeatCount = 0;
                long currentIndex = startIndex + 1;
                while (currentIndex < fileSize)
                {
                    byte currentByte = accessor.ReadByte(currentIndex);
                    if (currentByte == byteValue)
                    {
                        repeatCount++;
                        if (repeatCount >= minRepeatCount)
                        {
                            if (minRepeatCount == 0 ||
                                (currentIndex + 1 < fileSize && accessor.ReadByte(currentIndex + 1) != byteValue))
                            {
                                return currentIndex + 1;
                            }
                        }
                    }
                    else
                    {
                        repeatCount = 0;
                    }
                    currentIndex++;
                }
                return fileSize;
            }
        }
    }

    static void ExtractContent(string filePath, byte[] startSequenceBytes, byte[]? endSequence = null, string outputFormat = "bin",
                               string extractMode = "all", string? startAddress = null, string? endAddress = null, int minRepeatCount = 0)
    {
        outputFormat = outputFormat ?? "bin";

        try
        {
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;
            long startRange = 0;
            long endRange = fileSize;

            if (startAddress != null && endAddress != null)
            {
                long startIndex = Convert.ToInt64(startAddress.Replace("0x", ""), 16);
                long endIndex = Convert.ToInt64(endAddress.Replace("0x", ""), 16);
                if (startIndex > fileSize || endIndex > fileSize || startIndex > endIndex)
                {
                    Console.WriteLine($"指定地址范围 {startAddress}-{endAddress} 无效，无法提取。");
                    return;
                }
                startRange = startIndex;
                endRange = endIndex;
            }
            else if (startAddress != null)
            {
                long targetIndex = Convert.ToInt64(startAddress.Replace("0x", ""), 16);
                if (targetIndex > fileSize)
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
                    endRange = fileSize;
                }
                else
                {
                    Console.WriteLine("无效的提取模式参数");
                    return;
                }
            }

            int count = 0;
            List<string> notes = new List<string>();

            using (var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
            using (var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                long startIndexInContent = startRange;
                while (startIndexInContent < endRange)
                {
                    startIndexInContent = IndexOfSequence(accessor, startSequenceBytes, startIndexInContent, endRange);
                    if (startIndexInContent == -1)
                    {
                        Console.WriteLine($"No more start sequences found in {filePath}");
                        break;
                    }

                    long endIndexInContent = FindEndIndex(accessor, startIndexInContent, endSequence, minRepeatCount, startSequenceBytes, endRange);
                    endIndexInContent = Math.Min(endIndexInContent, endRange);

                    string newFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_{count}.{outputFormat}";
                    string directoryName = Path.GetDirectoryName(filePath) ?? ".";
                    string newFilePath = Path.Combine(directoryName, newFilename);

                    // Use streaming approach for large files
                    ExtractAndSaveSegment(filePath, newFilePath, startIndexInContent, endIndexInContent - startIndexInContent);

                    Console.WriteLine($"Extracted content saved as: {newFilePath}");
                    notes.Add($"File: {newFilePath}, Start Address: {startIndexInContent}, End Address: {endIndexInContent}");
                    count++;
                    startIndexInContent = endIndexInContent;
                }
            }

            SaveNotes(filePath, notes);
        }
        catch (Exception e)
        {
            Console.WriteLine($"处理文件 {filePath} 时出错，错误信息：{e}");
        }
    }

    static void ExtractAndSaveSegment(string sourcePath, string destPath, long startPosition, long length)
    {
        const int bufferSize = 1024 * 1024; // 1MB buffer
        var buffer = new byte[bufferSize];

        using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan))
        using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
        {
            sourceStream.Seek(startPosition, SeekOrigin.Begin);

            long bytesRemaining = length;
            while (bytesRemaining > 0)
            {
                int bytesToRead = (int)Math.Min(bufferSize, bytesRemaining);
                int bytesRead = sourceStream.Read(buffer, 0, bytesToRead);
                if (bytesRead == 0) break;

                destStream.Write(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }
        }
    }

    static void SaveNotes(string filePath, List<string> notes)
    {
        string notesFilename = $"{Path.GetFileNameWithoutExtension(filePath)}_notes.txt";
        string directoryName = Path.GetDirectoryName(filePath) ?? ".";
        string notesFilePath = Path.Combine(directoryName, notesFilename);
        File.WriteAllLines(notesFilePath, notes);
        Console.WriteLine($"Notes saved as: {notesFilePath}");
    }

    static long IndexOfSequence(MemoryMappedViewAccessor accessor, byte[] sequence, long startOffset, long maxOffset)
    {
        long maxSearchPosition = maxOffset - sequence.Length;
        if (startOffset > maxSearchPosition)
            return -1;

        byte firstByte = sequence[0];
        byte[] buffer = new byte[sequence.Length];

        for (long i = startOffset; i <= maxSearchPosition; i++)
        {
            byte currentByte = accessor.ReadByte(i);
            if (currentByte == firstByte)
            {
                bool match = true;
                for (int j = 1; j < sequence.Length; j++)
                {
                    if (accessor.ReadByte(i + j) != sequence[j])
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
