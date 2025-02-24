import os
import sys

def parse_start_sequence(start_sequence_input):
    if '*' in start_sequence_input:
        parts = start_sequence_input.split('*')
        byte_value = bytes.fromhex(parts[0].replace(' ', ''))
        repeat_count = int(parts[1])
        return byte_value * repeat_count
    else:
        return bytes.fromhex(start_sequence_input.replace(' ', ''))

def parse_end_sequence(end_sequence_input):
    parts = end_sequence_input.split()
    result = b""
    for part in parts:
        if '*' in part:
            sub_parts = part.split('*')
            byte_value = bytes.fromhex(sub_parts[0].replace(' ', ''))
            repeat_count = int(sub_parts[1])
            result += byte_value * repeat_count
        else:
            result += bytes.fromhex(part.replace(' ', ''))
    return result

def find_end_index(content, start_index, end_sequence, min_repeat_count, start_sequence_bytes):
    if end_sequence is None:
        next_start_index = content.find(start_sequence_bytes, start_index + 1)
        if next_start_index == -1:
            return len(content)
        else:
            return next_start_index
    else:
        if min_repeat_count == 0:
            end_index = content.find(end_sequence, start_index + 1)
            if end_index == -1:
                return len(content)
            else:
                return end_index + len(end_sequence)
        else:
            byte_value = end_sequence[0]
            repeat_count = 0
            current_index = start_index + 1
            while current_index < len(content):
                if content[current_index] == byte_value:
                    repeat_count += 1
                    if repeat_count >= min_repeat_count and (min_repeat_count == 0 or
                                                            content[current_index + 1] != byte_value):
                        return current_index + 1
                else:
                    repeat_count = 0
                current_index += 1
            return len(content)

def extract_content(file_path, start_sequence_bytes, end_sequence=None, output_format='bin',
                    extract_mode='all', start_address=None, end_address=None, min_repeat_count=0):
    try:
        with open(file_path, 'rb') as f:
            content = f.read()
    except IOError as e:
        print(f"无法读取文件 {file_path}，错误信息：{e}")
        return

    if start_address and end_address:
        start_index = int(start_address, 16)
        end_index = int(end_address, 16)
        if start_index > len(content) or end_index > len(content) or start_index > end_index:
            print(f"指定地址范围 {start_address}-{end_address} 无效，无法提取。")
            return
        start_range = start_index
        end_range = end_index
    elif start_address:
        target_index = int(start_address, 16)
        if target_index > len(content):
            print(f"指定地址 {start_address} 超出文件范围，无法提取。")
            return
        if extract_mode == 'before':
            start_range = 0
            end_range = target_index
        elif extract_mode == 'after':
            start_range = target_index
            end_range = len(content)
        else:
            print("无效的提取模式参数")
            return
    else:
        start_range = 0
        end_range = len(content)

    count = 0
    start_index = start_range
    notes = []
    while start_index < end_range:
        start_index = content.find(start_sequence_bytes, start_index)
        if start_index == -1:
            print(f"No more start sequences found in {file_path}")
            break

        end_index = find_end_index(content, start_index, end_sequence, min_repeat_count, start_sequence_bytes)
        end_index = min(end_index, end_range)

        extracted_data = content[start_index:end_index]
        new_filename = f"{os.path.splitext(os.path.basename(file_path))[0]}_{count}.{output_format}"
        new_filepath = os.path.join(os.path.dirname(file_path), new_filename)
        try:
            with open(new_filepath, 'wb') as new_file:
                new_file.write(extracted_data)
        except IOError as e:
            print(f"无法写入文件 {new_filepath}，错误信息：{e}")
            continue
        print(f"Extracted content saved as: {new_filepath}")

        notes.append(f"File: {new_filepath}, Start Address: {start_index}, End Address: {end_index}")
        count += 1
        start_index = end_index

    save_notes(file_path, notes)

def save_notes(file_path, notes):
    notes_filename = f"{os.path.splitext(os.path.basename(file_path))[0]}_notes.txt"
    notes_filepath = os.path.join(os.path.dirname(file_path), notes_filename)
    with open(notes_filepath, 'w') as notes_file:
        for note in notes:
            notes_file.write(note + '\n')
    print(f"Notes saved as: {notes_filepath}")

def main():
    directory_path = input("请输入要处理的文件夹路径: ")
    if not os.path.isdir(directory_path):
        print(f"错误: {directory_path} 不是一个有效的目录。")
        sys.exit(1)

    extract_mode = input("请选择提取模式（1:正常提取，2:提取指定地址前的内容，3:提取指定地址后的内容，4:从两个地址之间提取数据）: ")
    start_address = None
    end_address = None
    if extract_mode in ['2', '3']:
        start_address = input("请输入指定地址（例如: 0x00006F20）: ")
        if extract_mode == '2':
            extract_mode = 'before'
        else:
            extract_mode = 'after'
    elif extract_mode == '4':
        start_address = input("请输入起始地址（例如: 0x00006F20）: ")
        end_address = input("请输入结束地址（例如: 0x00007F20）: ")
        extract_mode = 'between'
    else:
        extract_mode = 'all'

    start_sequence_input = input("请输入起始序列的字节值，以空格分隔（也可输入类似00*16）: ")
    start_sequence_bytes = parse_start_sequence(start_sequence_input)
    end_sequence_input = input("请输入结束序列字节值（以空格分割，使用*表示重复，如00*4，直接回车跳过）: ")
    use_repeat_method = False
    min_repeat_count = 0
    if end_sequence_input:
        end_sequence_bytes = parse_end_sequence(end_sequence_input)
        if '*' not in end_sequence_input and len(end_sequence_bytes) == 1:
            try:
                min_repeat_count = int(input("请输入最小重复字节数量作为结束条件: "))
                use_repeat_method = True
            except:
                pass
    else:
        end_sequence_bytes = None
    output_format = input("请输入输出文件格式 (例如: bin): ")

    for root, dirs, files in os.walk(directory_path):
        for file in files:
            file_path = os.path.join(root, file)
            print(f"Processing file: {file_path}")
            extract_content(file_path, start_sequence_bytes, end_sequence_bytes, output_format,
                            extract_mode, start_address, end_address, min_repeat_count)

if __name__ == "__main__":
    main()