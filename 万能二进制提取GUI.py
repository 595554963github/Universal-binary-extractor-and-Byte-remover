import os
import tkinter as tk
from tkinter import filedialog

def extract_content(directory_path):
    for root, dirs, files in os.walk(directory_path):
        for file in files:
            file_path = os.path.join(root, file)
            # 从输入框获取起始序列和结束序列的十六进制字符串，并将其转换为字节序列
            start_sequence = bytes.fromhex(start_sequence_entry.get().replace(' ', ''))
            end_sequence = bytes.fromhex(end_sequence_entry.get().replace(' ', '')) if end_sequence_entry.get() else None
            output_format = output_format_entry.get()
            notes = []  # 初始化一个列表来存储每个提取片段的备注信息

            try:
                # 打开文件并读取内容
                with open(file_path, 'rb') as f:
                    content = f.read()
                    count = 0  # 用于生成新的文件名
                    start_index = 0  # 开始搜索起始序列的位置

                    # 遍历整个文件内容，寻找起始序列
                    while start_index < len(content):
                        start_index = content.find(start_sequence, start_index)
                        if start_index == -1:
                            print(f"在文件 {file_path} 中没有找到更多的起始序列")
                            break

                        # 如果设置了结束序列，则寻找结束序列；如果没有设置，则寻找下一个起始序列
                        if end_sequence:
                            end_index = content.find(end_sequence, start_index + len(start_sequence))
                            if end_index == -1:
                                end_index = len(content)
                            else:
                                end_index += len(end_sequence)
                        else:
                            end_index = content.find(start_sequence, start_index + len(start_sequence))
                            if end_index == -1:
                                end_index = len(content)

                        # 提取数据片段
                        extracted_data = content[start_index:end_index]

                        # 生成新的文件名并保存提取的数据
                        new_filename = f"{os.path.splitext(os.path.basename(file_path))[0]}_{count}.{output_format}"
                        new_filepath = os.path.join(os.path.dirname(file_path), new_filename)
                        with open(new_filepath, 'wb') as new_file:
                            new_file.write(extracted_data)
                        print(f"已保存提取的内容为: {new_filepath}")

                        # 将切割出来的每个文件的起始地址和结束地址添加到备注列表中
                        notes.append(f"文件: {new_filepath}, 起始地址: {start_index}, 结束地址: {end_index}")

                        count += 1  # 更新计数器
                        start_index = end_index  # 更新起始搜索位置

                # 将备注信息写入文本文件
                notes_filename = f"{os.path.splitext(os.path.basename(file_path))[0]}_notes.txt"
                notes_filepath = os.path.join(os.path.dirname(file_path), notes_filename)
                with open(notes_filepath, 'w') as notes_file:
                    for note in notes:
                        notes_file.write(note + '\n')
                print(f"备注信息已保存为: {notes_filepath}")

            except Exception as e:
                print(f"处理文件 {file_path} 时发生错误: {e}")

def select_directory():
    directory_path.set(filedialog.askdirectory())

# 创建主窗口
root = tk.Tk()
root.title("万能二进制内容提取器")
root.geometry("600x300")
root.resizable(True, True)

# 创建标签和输入框
label_directory = tk.Label(root, text="选择目录:")
label_directory.pack()
button_directory = tk.Button(root, text="选择文件夹", command=select_directory)
button_directory.pack()
directory_path = tk.StringVar()
entry_directory = tk.Entry(root, textvariable=directory_path)
entry_directory.pack()

label_start_sequence = tk.Label(root, text="起始序列（字节）:")
label_start_sequence.pack()
start_sequence_entry = tk.Entry(root, width=40)
start_sequence_entry.pack()

label_end_sequence = tk.Label(root, text="结束序列（字节，可选）:")
label_end_sequence.pack()
end_sequence_entry = tk.Entry(root, width=40)
end_sequence_entry.pack()

label_output_format = tk.Label(root, text="输出文件格式（例如：bin）:")
label_output_format.pack()
output_format_entry = tk.Entry(root, width=40)
output_format_entry.pack()

# 创建按钮并设置点击事件
button_extract = tk.Button(root, text="提取", command=lambda: extract_content(directory_path.get()))
button_extract.pack()

# 运行主循环
root.mainloop()
