import os
import tkinter as tk
from tkinter import filedialog, messagebox

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

                    sequence_index = content.find(byte_sequence) if position == 'first' else content.rfind(byte_sequence)
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

def browse_directory():
    directory_path = filedialog.askdirectory()
    if directory_path:
        directory_path_entry.delete(0, tk.END)
        directory_path_entry.insert(0, directory_path)

def process_files():
    try:
        directory_path = directory_path_entry.get()
        byte_sequence = bytes.fromhex(byte_sequence_entry.get().replace(' ', ''))
        new_extension = '.' + new_extension_entry.get()
        position = position_var.get()

        if not os.path.isdir(directory_path):
            raise ValueError("选择的路径不是一个目录或不存在。")

        remove_before_sequence(directory_path, byte_sequence, new_extension, position)
        messagebox.showinfo("完成", "文件处理完成。")

    except ValueError as ve:
        messagebox.showerror("错误", str(ve))
    except Exception as e:
        messagebox.showerror("错误", "处理文件时发生错误: " + str(e))

# 创建主窗口
root = tk.Tk()
root.title("文件处理器")
root.geometry("600x300")
root.resizable(True, True)

# 创建布局
# 路径选择
tk.Label(root, text="选择文件夹：").grid(row=0, column=0, padx=(10, 0), pady=10, sticky="w")
directory_path_entry = tk.Entry(root, width=30)
directory_path_entry.grid(row=0, column=1, padx=(10, 0), pady=10, sticky="ew")
browse_button = tk.Button(root, text="打开文件夹", command=browse_directory)
browse_button.grid(row=0, column=2, padx=(0, 10), pady=10)

# 字节序列输入
tk.Label(root, text="删除字节序列前面的所有内容（十六进制）：").grid(row=1, column=0, padx=(10, 0), pady=10, sticky="w")
byte_sequence_entry = tk.Entry(root, width=30)
byte_sequence_entry.grid(row=1, column=1, padx=(10, 0), pady=10, sticky="ew")

# 新扩展名输入
tk.Label(root, text="修改后缀名(文件头对应的格式)：").grid(row=2, column=0, padx=(10, 0), pady=10, sticky="w")
new_extension_entry = tk.Entry(root, width=30)
new_extension_entry.grid(row=2, column=1, padx=(10, 0), pady=10, sticky="ew")

# 查找位置选择
tk.Label(root, text="查找位置：").grid(row=3, column=0, padx=(10, 0), pady=10, sticky="w")
position_var = tk.StringVar(root)
position_var.set('first')
tk.Radiobutton(root, text="第一个", variable=position_var, value='first').grid(row=3, column=1, sticky="w")
tk.Radiobutton(root, text="最后一个", variable=position_var, value='last').grid(row=3, column=1, sticky="e")

# 处理文件按钮
process_button = tk.Button(root, text="处理文件", command=process_files)
process_button.grid(row=4, column=1, padx=(10, 0), pady=10)

# 运行主循环
root.mainloop()