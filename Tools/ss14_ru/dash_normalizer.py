import os
import re

from project import Project

RE_INNER_DASH = re.compile(r'(?<=\s)-(?=\s)')

def is_message_start(line: str) -> bool:
    stripped = line.lstrip()
    return '=' in stripped and not stripped.startswith(('#', '-', '.'))


def should_skip_line(line: str) -> bool:
    stripped = line.lstrip()
    return stripped.startswith('#') or stripped.startswith('-')


def transform_line_value(val: str) -> str:
    def replace_line(line: str) -> str:
        stripped = line.strip()
        if not stripped or stripped.startswith('-') or stripped.endswith('-'):
            return line

        end_pos = len(line.rstrip()) - 1
        return RE_INNER_DASH.sub(
            lambda m: '-' if m.start() == end_pos else '—',
            line
        )

    return ''.join(replace_line(l) for l in val.splitlines(keepends=True))


def process_file(filepath: str):
    with open(filepath, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    new_lines = []
    changed = False
    in_multiline = False

    for i, line in enumerate(lines):
        if should_skip_line(line):
            new_lines.append(line)
            in_multiline = False
            continue

        if is_message_start(line):
            key, sep, val = line.partition('=')
            val_stripped = val.strip()

            if not val_stripped or val_stripped.startswith('-') or val_stripped.endswith('-'):
                new_lines.append(line)
            else:
                new_val = RE_INNER_DASH.sub('—', val)
                if new_val != val:
                    changed = True
                new_lines.append(f"{key}{sep}{new_val}")

            in_multiline = True
            continue

        if in_multiline and line.startswith(' '):
            new_line = transform_line_value(line)
            if new_line != line:
                changed = True
            new_lines.append(new_line)
        else:
            new_lines.append(line)
            in_multiline = False

    if changed:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.writelines(new_lines)
        print(f"Обновлён: {filepath}")


def main():
    project = Project()
    root = project.ru_locale_dir_path

    for dirpath, _, files in os.walk(root):
        for name in files:
            if name.lower().endswith('.ftl'):
                process_file(os.path.join(dirpath, name))


if __name__ == '__main__':
    main()
