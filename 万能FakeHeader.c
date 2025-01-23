#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <dirent.h>
#include <sys/stat.h>
#include <errno.h>
#include <locale.h>
#ifdef _WIN32
#include <windows.h>
#endif

#define BUFFER_SIZE 1024

// 查找字节序列在内容中的位置
// position: 0 表示第一个，1 表示最后一个
long find_sequence(const unsigned char *content, size_t content_size, const unsigned char *byte_sequence, size_t seq_size, int position) {
    if (position == 0) { // first
        for (size_t i = 0; i <= content_size - seq_size; i++) {
            if (memcmp(content + i, byte_sequence, seq_size) == 0) {
                return i;
            }
        }
    }
    else if (position == 1) { // last
        for (long i = content_size - seq_size; i >= 0; i--) {
            if (memcmp(content + i, byte_sequence, seq_size) == 0) {
                return i;
            }
        }
    }
    return -1;
}

// 将路径连接
void join_path(char *result, const char *path1, const char *path2) {
    strcpy(result, path1);
    strcat(result, "/");
    strcat(result, path2);
}

// 检查路径是否为目录
int is_directory(const char *path) {
    struct stat statbuf;
    if (stat(path, &statbuf)!= 0)
        return 0;
    return S_ISDIR(statbuf.st_mode);
}

// 移除文件之前的内容，并重命名文件
void remove_before_sequence(const char *directory_path, const unsigned char *byte_sequence, size_t seq_size, const char *new_extension, int position) {
    DIR *dir;
    struct dirent *entry;
    int total_files_processed = 0;
    int total_files = 0;
    int total_folders_processed = 0;

    if (!is_directory(directory_path)) {
        fprintf(stderr, "提供的路径不是一个目录或不存在。\n");
        return;
    }

    dir = opendir(directory_path);
    if (!dir) {
        fprintf(stderr, "无法打开目录 %s: %s\n", directory_path, strerror(errno));
        return;
    }

    total_folders_processed += 1;

    while ((entry = readdir(dir))!= NULL) {
        // 跳过. 和..
        if (strcmp(entry->d_name, ".") == 0 || strcmp(entry->d_name, "..") == 0)
            continue;

        char path[BUFFER_SIZE];
        join_path(path, directory_path, entry->d_name);

#ifdef _WIN32
        // Windows下的目录判断
        struct stat path_stat;
        stat(path, &path_stat);
        if (S_ISDIR(path_stat.st_mode)) {
#else
        // POSIX系统下的目录判断
        if (is_directory(path)) {
#endif
            // 递归处理子目录
            remove_before_sequence(path, byte_sequence, seq_size, new_extension, position);
            total_folders_processed += 1;
        }
        else {
            total_files += 1;
            // 跳过以.py结尾的文件
            size_t len = strlen(entry->d_name);
            if (len >= 3 && strcmp(entry->d_name + len - 3, ".py") == 0)
                continue;

            // 跳过名称中包含 "disabled" 的文件（不区分大小写）
            char lower_name[BUFFER_SIZE];
            strncpy(lower_name, entry->d_name, BUFFER_SIZE);
            for (int i = 0; lower_name[i]; i++) {
                if (lower_name[i] >= 'A' && lower_name[i] <= 'Z')
                    lower_name[i] = lower_name[i] + 'a' - 'A';
            }
            if (strstr(lower_name, "disabled")!= NULL)
                continue;

            // 处理文件
            FILE *file = fopen(path, "rb");
            if (!file) {
                fprintf(stderr, "处理文件 %s 时出错: %s\n", path, strerror(errno));
                continue;
            }

            // 获取文件大小
            fseek(file, 0, SEEK_END);
            long file_size = ftell(file);
            fseek(file, 0, SEEK_SET);

            unsigned char *content = (unsigned char *)malloc(file_size);
            if (!content) {
                fprintf(stderr, "内存分配失败。\n");
                fclose(file);
                continue;
            }

            fread(content, 1, file_size, file);
            fclose(file);

            long sequence_index = find_sequence(content, file_size, byte_sequence, seq_size, position);
            if (sequence_index!= -1) {
                size_t new_size = file_size - sequence_index;
                unsigned char *new_content = (unsigned char *)malloc(new_size);
                if (!new_content) {
                    fprintf(stderr, "内存分配失败。\n");
                    free(content);
                    continue;
                }
                memcpy(new_content, content + sequence_index, new_size);
                free(content);

                file = fopen(path, "wb");
                if (!file) {
                    fprintf(stderr, "无法写入文件 %s: %s\n", path, strerror(errno));
                    free(new_content);
                    continue;
                }
                fwrite(new_content, 1, new_size, file);
                fclose(file);
                free(new_content);

                // 构造新的文件名
                char new_file_name[BUFFER_SIZE];
                strcpy(new_file_name, entry->d_name);
                char *dot = strrchr(new_file_name, '.');
                if (dot)
                    *dot = '\0';
                strcat(new_file_name, new_extension);

                char new_file_path[BUFFER_SIZE];
                join_path(new_file_path, directory_path, new_file_name);

                // 重命名文件
                if (rename(path, new_file_path) == 0) {
                    printf("已重命名文件为: %s\n", new_file_path);
                }
                else {
                    printf("文件 %s 已存在，未重命名。\n", new_file_path);
                }

                total_files_processed += 1;
                double progress = ((double)total_files_processed / total_files) * 100.0;
                printf("\r重命名进度: %.2f%%", progress);
                fflush(stdout);
            }
            else {
                free(content);
            }
        }
    }

    closedir(dir);
    printf("\n处理了 %d 个文件.\n", total_files_processed);
    printf("处理了 %d 个文件夹.\n", total_folders_processed);
}

int main() {
#ifdef _WIN32
    // 设置控制台输出为UTF - 8
    SetConsoleOutputCP(65001);
#else
    // 非Windows系统设置locale为UTF - 8
    setlocale(LC_ALL, "");
#endif

    char directory_path[BUFFER_SIZE];
    printf("请输入要处理的文件夹路径: ");
    if (fgets(directory_path, sizeof(directory_path), stdin) == NULL) {
        fprintf(stderr, "输入错误。\n");
        return 1;
    }
    // 移除换行符
    directory_path[strcspn(directory_path, "\n")] = 0;

    char byte_sequence_input[BUFFER_SIZE];
    printf("请输入指定的字节序列（以十六进制形式，例如：'50554946' 或 '50 55 49 46'）: ");
    if (fgets(byte_sequence_input, sizeof(byte_sequence_input), stdin) == NULL) {
        fprintf(stderr, "输入错误。\n");
        return 1;
    }
    byte_sequence_input[strcspn(byte_sequence_input, "\n")] = 0;

    // 移除空格
    char hex_str[BUFFER_SIZE];
    int j = 0;
    for (int i = 0; byte_sequence_input[i]!= '\0'; i++) {
        if (byte_sequence_input[i]!= ' ')
            hex_str[j++] = byte_sequence_input[i];
    }
    hex_str[j] = '\0';

    // 验证十六进制字符串长度
    size_t hex_len = strlen(hex_str);
    if (hex_len % 2!= 0) {
        fprintf(stderr, "字节序列长度无效。\n");
        return 1;
    }

    size_t byte_seq_size = hex_len / 2;
    unsigned char *byte_sequence = (unsigned char *)malloc(byte_seq_size);
    if (!byte_sequence) {
        fprintf(stderr, "内存分配失败。\n");
        return 1;
    }

    for (size_t i = 0; i < byte_seq_size; i++) {
        sscanf(&hex_str[i * 2], "%2hhx", &byte_sequence[i]);
    }

    char new_extension_input[BUFFER_SIZE];
    printf("请输入新的文件扩展名（例如：'acb'）: ");
    if (fgets(new_extension_input, sizeof(new_extension_input), stdin) == NULL) {
        fprintf(stderr, "输入错误。\n");
        free(byte_sequence);
        return 1;
    }
    new_extension_input[strcspn(new_extension_input, "\n")] = 0;

    char new_extension[BUFFER_SIZE];
    strcpy(new_extension, ".");
    strcat(new_extension, new_extension_input);

    char position_input[BUFFER_SIZE];
    printf("请选择查找位置（1：第一个，2：最后一个）: ");
    if (fgets(position_input, sizeof(position_input), stdin) == NULL) {
        fprintf(stderr, "输入错误。\n");
        free(byte_sequence);
        return 1;
    }

    int position = 0; // 0 for first, 1 for last
    if (strncmp(position_input, "1", 1) == 0)
        position = 0;
    else
        position = 1;

    remove_before_sequence(directory_path, byte_sequence, byte_seq_size, new_extension, position);

    free(byte_sequence);
    return 0;
}