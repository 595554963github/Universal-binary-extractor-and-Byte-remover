import os

def find_sequence(content, byte_sequence, position='first'):
    if position == 'first':
        return content.find(byte_sequence)
    elif position == 'last':
        return content.rfind(byte_sequence)
    return -1

def remove_before_sequence(directory_path, byte_sequence, new_extension, position='first'):
    total_files_processed = 0
    total_files = 0
    total_folders_processed = 0

    try:
        if not os.path.isdir(directory_path):
            raise ValueError("提供的路径不是一个目录或不存在。")

        for root, dirs, files in os.walk(directory_path):
            total_folders_processed += 1
            for file_name in files:
                if file_name.endswith('.py'):
                    continue
                if "disabled" in file_name.lower():
                    continue
                file_path = os.path.join(root, file_name)

                try:
                    with open(file_path, 'rb') as file:
                        content = file.read()

                    sequence_index = find_sequence(content, byte_sequence, position)
                    if sequence_index != -1:
                        new_content = content[sequence_index:]
                        with open(file_path, 'wb') as file:
                            file.write(new_content)
                        new_file_name = os.path.splitext(file_name)[0] + new_extension
                        new_file_path = os.path.join(root, new_file_name)
                        if not os.path.exists(new_file_path):
                            os.rename(file_path, new_file_path)
                            print(f"已重命名文件为: {new_file_path}")
                        else:
                            print(f"文件 {new_file_path} 已存在，未重命名。")
                        total_files_processed += 1

                except Exception as e:
                    print(f"处理文件 {file_path} 时出错: {e}")

    except Exception as e:
        print(f"错误: {e}")

    print(f"处理了 {total_files_processed} 个文件.")
    print(f"处理了 {total_folders_processed} 个文件夹.")

# 用户输入要处理的文件夹路径
directory_path = input("请输入要处理的文件夹路径: ")

# 处理字节序列输入
byte_sequence_input = input("请输入指定的字节序列（以十六进制形式，例如：'50554946' 或 '50 55 49 46'）: ")
byte_sequence = bytes.fromhex(byte_sequence_input.replace(' ', ''))  # 移除可能存在的空格

# 用户输入新的文件扩展名，不需要输入点（"."）
new_extension_input = input("请输入新的文件扩展名（例如：'acb'）: ")
new_extension = '.' + new_extension_input  # 自动在前面加上点（"."）

# 用户选择查找位置
position_input = input("请选择查找位置（1：第一个，2：最后一个）: ")
position = 'first' if position_input.strip() == '1' else 'last'

remove_before_sequence(directory_path, byte_sequence, new_extension, position)
