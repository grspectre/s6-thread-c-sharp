import os

# Значимые расширения файлов для .NET проекта
DOTNET_EXTENSIONS = {
    # Исходный код
    ".cs", ".fs", ".vb",
    # Проекты и решения
    ".csproj", ".fsproj", ".vbproj", ".sln",
    # Конфигурация
    ".json", ".xml", ".yaml", ".yml", ".config", ".env",
    # Веб
    ".cshtml", ".razor", ".html", ".css", ".js", ".ts",
    # Ресурсы
    ".resx",
    # Прочее
    ".md", ".txt", ".proto", ".graphql", ".sql"
}

# Папки, которые нужно игнорировать
IGNORE_DIRS = {
    "bin", "obj", ".git", ".vs", ".idea",
    "node_modules", ".github", "packages",
    "TestResults", ".nuget"
}

# Файлы, которые нужно игнорировать
IGNORE_FILES = {
    ".gitignore", ".gitattributes", ".editorconfig",
    "Thumbs.db", ".DS_Store"
}


def collect_dotnet_files(
    project_path: str,
    output_file: str = "project_files.txt",
    include_content: bool = False
) -> None:
    """
    Обходит .NET проект и записывает все значимые файлы в текстовый файл.

    :param project_path:   Путь к корню проекта.
    :param output_file:    Путь к выходному текстовому файлу.
    :param include_content: Если True — записывает содержимое каждого файла.
    """
    project_path = os.path.abspath(project_path)
    collected: list[str] = []

    for root, dirs, files in os.walk(project_path):
        # Фильтруем игнорируемые папки (изменяем dirs на месте, чтобы os.walk не заходил в них)
        dirs[:] = [d for d in dirs if d not in IGNORE_DIRS and not d.startswith(".")]

        for file_name in sorted(files):
            # Пропускаем игнорируемые файлы
            if file_name in IGNORE_FILES:
                continue

            _, ext = os.path.splitext(file_name)
            if ext.lower() not in DOTNET_EXTENSIONS:
                continue

            full_path = os.path.join(root, file_name)
            # Относительный путь для читаемости
            rel_path = os.path.relpath(full_path, project_path)
            collected.append(rel_path)

    with open(output_file, "w", encoding="utf-8") as out:
        out.write(f"Проект: {project_path}\n")
        out.write(f"Найдено файлов: {len(collected)}\n")
        out.write("=" * 60 + "\n\n")

        for rel_path in collected:
            out.write(rel_path + "\n")

            if include_content:
                full_path = os.path.join(project_path, rel_path)
                out.write("-" * 40 + "\n")
                try:
                    with open(full_path, "r", encoding="utf-8") as f:
                        out.write(f.read())
                except (UnicodeDecodeError, PermissionError) as e:
                    out.write(f"[Не удалось прочитать файл: {e}]\n")
                out.write("\n" + "=" * 60 + "\n\n")

    print(f"Готово! Записано {len(collected)} файлов → {output_file}")


# ── Пример использования ──────────────────────────────────────
if __name__ == "__main__":
    # Только список файлов
    collect_dotnet_files(
        project_path=r"/Users/ant/learn/s6-thread-c-sharp/ThreadPool/",
        output_file="content.txt",
        include_content=True
    )

    # Список файлов + их содержимое
    # collect_dotnet_files(
    #     project_path=r"C:\Projects\MyDotNetApp",
    #     output_file="files_with_content.txt",
    #     include_content=True
    # )