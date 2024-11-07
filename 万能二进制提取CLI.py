import os
import sys

def extract_content(file_path, start_sequence, end_sequence=None, output_format='bin'):
    with open(file_path, 'rb') as f:
        content = f.read()
        count = 0
        start_index = 0
        notes = []  # 初始化一个列表来存储备注信息

        while start_index < len(content):
            start_index = content.find(start_sequence, start_index)
            if start_index == -1:
                print(f"No more start sequences found in {file_path}")
                break

            if end_sequence:
                end_index = content.find(end_sequence, start_index + len(start_sequence))
                if end_index == -1:
                    end_index = len(content)
                else:
                    end_index += len(end_sequence)
            else:
                next_start_index = content.find(start_sequence, start_index + len(start_sequence))
                end_index = next_start_index if next_start_index != -1 else len(content)

            extracted_data = content[start_index:end_index]
            new_filename = f"{os.path.splitext(os.path.basename(file_path))[0]}_{count}.{output_format}"
            new_filepath = os.path.join(os.path.dirname(file_path), new_filename)
            with open(new_filepath, 'wb') as new_file:
                new_file.write(extracted_data)
            print(f"Extracted content saved as: {new_filepath}")

            # 将切割出来的每个文件的起始地址和结束地址添加到备注列表中
            notes.append(f"File: {new_filepath}, Start Address: {start_index}, End Address: {end_index}")

            count += 1
            start_index = end_index

        # 写入备注信息到txt文件
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

    start_sequence_input = input("请输入起始序列的字节值，以空格分隔: ")
    start_sequence_bytes = bytes.fromhex(start_sequence_input.replace(' ', ''))

    end_sequence_input = input("请输入结束序列的字节值，以空格分隔 (直接回车跳过): ")
    end_sequence_bytes = bytes.fromhex(end_sequence_input.replace(' ', '')) if end_sequence_input else None

    output_format = input("请输入输出文件格式 (例如: bin): ")

    for root, dirs, files in os.walk(directory_path):
        for file in files:
            file_path = os.path.join(root, file)
            print(f"Processing file: {file_path}")
            extract_content(file_path, start_sequence_bytes, end_sequence_bytes, output_format)

if __name__ == "__main__":
    main()
