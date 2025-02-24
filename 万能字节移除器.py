import os
import sys

def find_all_sequences(content, byte_sequence):
    indices = []
    index = content.find(byte_sequence)
    while index != -1:
        indices.append(index)
        index = content.find(byte_sequence, index + 1)
    return indices

def process_files(directory_path, process_func, *args):
    total_files_processed = 0
    total_files = 0
    total_folders_processed = 0

    try:
        if not os.path.isdir(directory_path):
            raise ValueError("提供的路径不是一个目录或不存在。")

        for root, dirs, files in os.walk(directory_path):
            total_folders_processed += 1
            total_files += len(files)
            for file_name in files:
                if file_name.endswith('.py'):
                    continue
                if "disabled" in file_name.lower():
                    continue
                file_path = os.path.join(root, file_name)
                try:
                    result = process_func(file_path, *args)
                    if result:
                        total_files_processed += 1
                        progress = (total_files_processed / total_files) * 100
                        print(f"\r重命名进度: {progress:.2f}%", end='')
                        sys.stdout.flush()
                except Exception as e:
                    print(f"处理文件 {file_path} 时出错: {e}")

    except Exception as e:
        print(f"错误: {e}")

    print(f"\n处理了 {total_files_processed} 个文件.")
    print(f"处理了 {total_folders_processed} 个文件夹.")

def remove_before_seq(file_path, byte_sequence, new_extension, seq_num):
    with open(file_path, 'rb') as file:
        content = file.read()
    all_indices = find_all_sequences(content, byte_sequence)
    if len(all_indices) >= seq_num:
        sequence_index = all_indices[seq_num - 1]
        new_content = content[sequence_index:]
        with open(file_path, 'wb') as file:
            file.write(new_content)
        new_file_name = os.path.splitext(os.path.basename(file_path))[0] + '.' + new_extension
        new_file_path = os.path.join(os.path.dirname(file_path), new_file_name)
        if not os.path.exists(new_file_path):
            os.rename(file_path, new_file_path)
            print(f"已重命名文件为: {new_file_path}")
        else:
            print(f"文件 {new_file_path} 已存在，未重命名。")
        return True
    return False

def remove_before_addr(file_path, address, new_extension):
    with open(file_path, 'rb') as file:
        content = file.read()
    if address < len(content):
        new_content = content[address:]
        with open(file_path, 'wb') as file:
            file.write(new_content)
        new_file_name = os.path.splitext(os.path.basename(file_path))[0] + '.' + new_extension
        new_file_path = os.path.join(os.path.dirname(file_path), new_file_name)
        if not os.path.exists(new_file_path):
            os.rename(file_path, new_file_path)
            print(f"已重命名文件为: {new_file_path}")
        else:
            print(f"文件 {new_file_path} 已存在，未重命名。")
        return True
    return False

def remove_after_addr(file_path, address, new_extension):
    with open(file_path, 'rb') as file:
        content = file.read()
    if address < len(content):
        new_content = content[:address]
        with open(file_path, 'wb') as file:
            file.write(new_content)
        new_file_name = os.path.splitext(os.path.basename(file_path))[0] + '.' + new_extension
        new_file_path = os.path.join(os.path.dirname(file_path), new_file_name)
        if not os.path.exists(new_file_path):
            os.rename(file_path, new_file_path)
            print(f"已重命名文件为: {new_file_path}")
        else:
            print(f"文件 {new_file_path} 已存在，未重命名。")
        return True
    return False

def remove_outside_addrs(file_path, start_address, end_address, new_extension):
    with open(file_path, 'rb') as file:
        content = file.read()
    if start_address < len(content) and end_address < len(content) and start_address < end_address:
        new_content = content[start_address:end_address]
        with open(file_path, 'wb') as file:
            file.write(new_content)
        new_file_name = os.path.splitext(os.path.basename(file_path))[0] + '.' + new_extension
        new_file_path = os.path.join(os.path.dirname(file_path), new_file_name)
        if not os.path.exists(new_file_path):
            os.rename(file_path, new_file_path)
            print(f"已重命名文件为: {new_file_path}")
        else:
            print(f"文件 {new_file_path} 已存在，未重命名。")
        return True
    return False

# 用户输入要处理的文件夹路径
directory_path = input("请输入要处理的文件夹路径: ")

# 用户选择模式
mode = input("请选择模式（1：删除指定字节序列前面的所有字节，2：删除指定地址前面的所有字节，3：删除指定地址后的所有字节，4：删除起始地址前面和结束地址后面的所有字节）: ")

if mode == '1':
    # 处理字节序列输入
    byte_sequence_input = input("请输入指定的字节序列（以十六进制形式，例如：'50554946' 或 '50 55 49 46'）: ")
    byte_sequence = bytes.fromhex(byte_sequence_input.replace(' ', ''))

    for root, dirs, files in os.walk(directory_path):
        for file_name in files:
            if file_name.endswith('.py'):
                continue
            if "disabled" in file_name.lower():
                continue
            file_path = os.path.join(root, file_name)
            try:
                with open(file_path, 'rb') as file:
                    content = file.read()
                all_indices = find_all_sequences(content, byte_sequence)
                print(f"文件 {file_path} 中找到 {len(all_indices)} 个指定字节序列。")
            except Exception as e:
                print(f"读取文件 {file_path} 时出错: {e}")

    while True:
        try:
            seq_num = int(input("请选择从第几个字节序列前面删除多余字节（从 1 开始）: "))
            if seq_num < 1:
                print("数量不能小于 1，请重新输入。")
            elif seq_num > len(all_indices):
                print(f"数量不能超过找到的最大字节序列数量 {len(all_indices)}，请重新输入。")
            else:
                break
        except ValueError:
            print("输入无效，请输入一个整数。")

    # 用户输入新的文件扩展名，不需要输入点（"."）
    new_extension_input = input("请输入新的文件扩展名（例如：'acb'）: ")
    new_extension = new_extension_input
    process_files(directory_path, remove_before_seq, byte_sequence, new_extension, seq_num)
elif mode in ('2', '3', '4'):
    if mode == '2':
        address = int(input("请输入指定地址（例如：0x00006F20）: "), 16)
    elif mode == '3':
        address = int(input("请输入指定地址（例如：0x00006F20）: "), 16)
    elif mode == '4':
        start_address = int(input("请输入起始地址（例如：0x00006F20）: "), 16)
        end_address = int(input("请输入结束地址（例如：0x00007F20）: "), 16)

    # 用户输入新的文件扩展名，不需要输入点（"."）
    new_extension_input = input("请输入新的文件扩展名（例如：'acb'）: ")
    new_extension = new_extension_input

    if mode == '2':
        process_files(directory_path, remove_before_addr, address, new_extension)
    elif mode == '3':
        process_files(directory_path, remove_after_addr, address, new_extension)
    elif mode == '4':
        process_files(directory_path, remove_outside_addrs, start_address, end_address, new_extension)
else:
    print("无效的模式选择")