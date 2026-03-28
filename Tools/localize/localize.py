#!/usr/bin/env python3
# Автор инструмента discord (metalsage <@494907100376596480>)

import argparse
import logging
import multiprocessing
import os
import re
import sys
from concurrent.futures import ProcessPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

try:
    import yaml

    try:
        from yaml import CSafeLoader as SafeLoader
    except ImportError:
        from yaml import SafeLoader
except ImportError:
    print("Ошибка: Библиотека 'pyyaml' не установлена, но она обязательна для работы.")
    print("Установите её с помощью команды: pip install pyyaml")
    sys.exit(1)


class SS14YamlLoader(SafeLoader):
    pass


def construct_undefined(loader, tag_suffix, node):
    if isinstance(node, yaml.ScalarNode):
        return loader.construct_scalar(node)
    elif isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node)
    elif isinstance(node, yaml.MappingNode):
        return loader.construct_mapping(node)
    return None


SS14YamlLoader.add_multi_constructor("!", construct_undefined)


@dataclass(slots=True)
class FtlAttribute:
    name: str
    value: str
    key_space: str = " "
    val_space: str = " "
    indent: str = "    "


@dataclass(slots=True)
class FtlEntry:
    key: str
    value: str
    attributes: Dict[str, FtlAttribute] = field(default_factory=dict)
    ignore_untranslated: bool = False
    variables: Set[str] = field(default_factory=set)
    preceding_text: List[str] = field(default_factory=list)
    key_space: str = " "
    val_space: str = " "


@dataclass(slots=True)
class PrototypeEntry:
    id: str
    parents: List[str]
    name: Optional[str] = None
    desc: Optional[str] = None
    suffix: Optional[str] = None

    name_parent: Optional[str] = None
    desc_parent: Optional[str] = None
    suffix_parent: Optional[str] = None

    resolved_name: Optional[str] = None
    resolved_desc: Optional[str] = None
    resolved_suffix: Optional[str] = None

    filepath: str = ""
    file_index: int = 0


class RunContext:
    def __init__(
        self,
        ignore_paths: Optional[List[Path]] = None,
        ignore_untranslated_paths: Optional[List[Path]] = None,
    ):
        self._processed_files: Set[str] = set()
        self.parsed_ftl_cache: Dict[
            Path, Tuple[Dict[str, FtlEntry], List[str], bool]
        ] = {}

        self.ignore_paths: List[str] = [
            os.path.normcase(os.path.abspath(p)) for p in (ignore_paths or [])
        ]
        self.ignore_untranslated_paths: List[str] = [
            os.path.normcase(os.path.abspath(p))
            for p in (ignore_untranslated_paths or [])
        ]

    def mark_processed(self, filepath: Path) -> bool:
        path_str = str(filepath.resolve())
        if path_str in self._processed_files:
            return True
        self._processed_files.add(path_str)
        return False

    def is_ignored(self, filepath: Path) -> bool:
        if not self.ignore_paths:
            return False

        file_abs = os.path.normcase(os.path.abspath(filepath))
        for ign in self.ignore_paths:
            if file_abs == ign or file_abs.startswith(ign + os.sep):
                return True
        return False

    def is_untranslated_ignored(self, filepath: Path) -> bool:
        if not self.ignore_untranslated_paths:
            return False

        file_abs = os.path.normcase(os.path.abspath(filepath))
        for ign in self.ignore_untranslated_paths:
            if file_abs == ign or file_abs.startswith(ign + os.sep):
                return True
        return False


class FtlParser:
    VAR_REGEX = re.compile(r"\$([a-zA-Z0-9_-]+)")
    REF_REGEX = re.compile(r"\{[^}]+\}")
    TAG_REGEX = re.compile(r"\[[^\]]+\]")

    MSG_DEF_RE = re.compile(r"^-?[a-zA-Z][a-zA-Z0-9_-]*\s*=")
    ATTR_DEF_RE = re.compile(r"^\s+\.[a-zA-Z][a-zA-Z0-9_-]*\s*=")
    COMMENT_RE = re.compile(r"^\s*#")

    @classmethod
    def parse_file(cls, filepath: Path) -> Tuple[Dict[str, FtlEntry], List[str], bool]:
        entries: Dict[str, FtlEntry] = {}
        buffer: List[str] = []
        pending_blanks: List[str] = []
        ends_with_newline = False

        try:
            with open(filepath, "r", encoding="utf-8-sig") as f:
                current_entry: Optional[FtlEntry] = None
                current_attr: Optional[FtlAttribute] = None

                for line in f:
                    raw_line = line.replace("\ufeff", "")
                    ends_with_newline = raw_line.endswith("\n")
                    stripped = raw_line.strip()

                    if not stripped:
                        pending_blanks.append(raw_line.rstrip("\n"))
                        continue

                    if cls.COMMENT_RE.match(raw_line):
                        current_entry, current_attr = None, None
                        buffer.extend(pending_blanks)
                        pending_blanks.clear()
                        buffer.append(raw_line.rstrip("\n"))
                        continue

                    if cls.MSG_DEF_RE.match(raw_line):
                        current_entry, current_attr = None, None
                        buffer.extend(pending_blanks)
                        pending_blanks.clear()

                        key_part, val_part = raw_line.split("=", 1)

                        key = key_part.strip()
                        key_space_match = re.search(r"([ \t]+)$", key_part)
                        key_space = key_space_match.group(1) if key_space_match else ""

                        val_str = val_part.rstrip("\n")
                        val_space_match = re.match(r"^([ \t]+)", val_part)
                        val_space = val_space_match.group(1) if val_space_match else ""
                        if val_space:
                            val_str = val_str[len(val_space) :]

                        ignore_next = any(
                            "ignore-untranslated" in b.lower() for b in buffer
                        )

                        current_entry = FtlEntry(
                            key=key,
                            value=val_str,
                            ignore_untranslated=ignore_next,
                            variables=set(cls.VAR_REGEX.findall(val_str)),
                            preceding_text=buffer.copy(),
                            key_space=key_space,
                            val_space=val_space,
                        )
                        entries[key] = current_entry
                        buffer.clear()
                        continue

                    if current_entry and cls.ATTR_DEF_RE.match(raw_line):
                        append_str = ""
                        for b in pending_blanks:
                            append_str += "\n" + b.rstrip("\n")

                        if current_attr:
                            current_attr.value += append_str
                        else:
                            current_entry.value += append_str
                        pending_blanks.clear()

                        attr_parts = raw_line.split("=", 1)
                        attr_key_part = attr_parts[0]
                        attr_val_part = attr_parts[1]

                        indent_match = re.match(r"^([ \t]+)", attr_key_part)
                        indent = indent_match.group(1) if indent_match else "    "

                        attr_name_full = attr_key_part.strip()
                        attr_name = attr_name_full[1:]

                        key_space_match = re.search(r"([ \t]+)$", attr_key_part)
                        key_space = key_space_match.group(1) if key_space_match else ""

                        attr_val_str = attr_val_part.rstrip("\n")
                        val_space_match = re.match(r"^([ \t]+)", attr_val_part)
                        val_space = val_space_match.group(1) if val_space_match else ""
                        if val_space:
                            attr_val_str = attr_val_str[len(val_space) :]

                        current_attr = FtlAttribute(
                            attr_name, attr_val_str, key_space, val_space, indent
                        )
                        current_entry.attributes[attr_name] = current_attr
                        current_entry.variables.update(
                            cls.VAR_REGEX.findall(attr_val_str)
                        )
                        continue

                    if current_entry:
                        append_str = ""
                        for b in pending_blanks:
                            append_str += "\n" + b.rstrip("\n")
                        append_str += "\n" + raw_line.rstrip("\n")
                        pending_blanks.clear()

                        target = current_attr if current_attr else current_entry
                        target.value += append_str
                        current_entry.variables.update(cls.VAR_REGEX.findall(raw_line))
                        continue

                    current_entry, current_attr = None, None
                    buffer.extend(pending_blanks)
                    pending_blanks.clear()
                    buffer.append(raw_line.rstrip("\n"))

        except OSError as e:
            logging.error(f"Ошибка чтения fluent файла {filepath}: {e}")

        buffer.extend(pending_blanks)
        return entries, buffer, ends_with_newline

    @classmethod
    def _format_value(
        cls, key: str, value: str, indent: str, key_space: str, val_space: str
    ) -> str:
        if value is None:
            value = ""

        val_str = str(value)
        lines = val_str.split("\n")
        result_lines = []

        first_line = lines[0]
        if not first_line.strip() and len(lines) > 1:
            result_lines.append(f"{indent}{key}{key_space}={val_space}")
        else:
            result_lines.append(f"{indent}{key}{key_space}={val_space}{first_line}")

        for line in lines[1:]:
            if not line.strip():
                result_lines.append("")
            else:
                if (
                    not line.startswith(" ")
                    and not line.startswith("\t")
                    and not line.startswith("}")
                ):
                    result_lines.append(f"{indent}    {line}")
                else:
                    result_lines.append(line)

        return "\n".join(result_lines)

    @classmethod
    def write_file(
        cls,
        filepath: Path,
        entries: Dict[str, FtlEntry],
        trailing_text: List[str],
        ends_with_newline: bool = True,
    ) -> None:
        try:
            filepath.parent.mkdir(parents=True, exist_ok=True)
            lines_to_write = []

            for entry in entries.values():
                has_value = bool(entry.value and str(entry.value).strip())
                has_attrs = any(
                    attr.value and str(attr.value).strip()
                    for attr in entry.attributes.values()
                )

                if not has_value and not has_attrs:
                    continue

                lines_to_write.extend(entry.preceding_text)

                val_str = cls._format_value(
                    entry.key, entry.value, "", entry.key_space, entry.val_space
                )
                lines_to_write.append(val_str)

                for attr in entry.attributes.values():
                    if attr.name == "desc" or (attr.value and str(attr.value).strip()):
                        attr_str = cls._format_value(
                            "." + attr.name,
                            attr.value,
                            attr.indent,
                            attr.key_space,
                            attr.val_space,
                        )
                        lines_to_write.append(attr_str)

            lines_to_write.extend(trailing_text)

            content = "\n".join(lines_to_write)
            if not content.strip():
                if filepath.exists():
                    try:
                        filepath.unlink()
                    except OSError:
                        pass
                return

            if ends_with_newline:
                content += "\n"

            with open(filepath, "w", encoding="utf-8") as f:
                f.write(content)

        except OSError as e:
            logging.error(f"Ошибка записи fluent файла {filepath}: {e}")


class LocalizationManager:
    CYRILLIC_PATTERN = re.compile(r"[А-Яа-яЁё]")
    LATIN_PATTERN = re.compile(r"[a-zA-Z]")
    RE_INNER_DASH = re.compile(r"(?<=\s)-(?=\s)")

    def __init__(self, root_dir: Path, format_level: str, config_file: str):
        self.root_dir = root_dir
        self.format_level = format_level
        self.resources_dir = self.root_dir / "Resources"
        self.locales_dir = self.resources_dir / "Locale"
        self.en_us_dir = self.locales_dir / "en-US"
        self.ru_ru_dir = self.locales_dir / "ru-RU"
        self.robust_en_dir = (
            self.root_dir / "RobustToolbox" / "Resources" / "Locale" / "en-US"
        )
        self.robust_ru_dir = self.ru_ru_dir / "robust-toolbox"
        self.prototypes_ru_dir = self.ru_ru_dir / "ss14-ru" / "prototypes"
        self.prototypes_src_dir = self.resources_dir / "Prototypes"

        ignore_paths, ignore_untranslated = self._load_config(config_file)
        self.context = RunContext(ignore_paths, ignore_untranslated)

    def _load_config(self, config_file: str) -> Tuple[List[Path], List[Path]]:
        ignore_paths: List[Path] = []
        ignore_untranslated_paths: List[Path] = []

        config_path = Path(config_file).resolve()
        if not config_path.exists():
            config_path = (self.root_dir / config_file).resolve()

        if config_path.exists():
            try:
                with open(config_path, "r", encoding="utf-8") as f:
                    config_data = yaml.safe_load(f)
                    if config_data and isinstance(config_data, dict):
                        for p in config_data.get("ignored_paths", []):
                            ignore_paths.append((self.root_dir / p).resolve())
                        for p in config_data.get("ignored_untranslated_paths", []):
                            ignore_untranslated_paths.append(
                                (self.root_dir / p).resolve()
                            )
                logging.info(f"Конфигурация загружена из {config_path.name}")
            except Exception as e:
                logging.error(f"Ошибка чтения конфигурации {config_path}: {e}")
        else:
            logging.info(
                f"Конфигурационный файл {config_file} не найден. "
                "Игнорирование путей отключено."
            )

        return ignore_paths, ignore_untranslated_paths

    def _get_parsed_ftl(
        self, filepath: Path
    ) -> Tuple[Dict[str, FtlEntry], List[str], bool]:
        if filepath not in self.context.parsed_ftl_cache:
            self.context.parsed_ftl_cache[filepath] = FtlParser.parse_file(filepath)
        return self.context.parsed_ftl_cache[filepath]

    def sync_systems(self) -> None:
        global_memory = self._load_translation_memory()

        valid_ru_files = self._sync_directories(
            self.en_us_dir, self.ru_ru_dir, global_memory
        )
        ignore_dirs_ru = [self.robust_ru_dir, self.ru_ru_dir / "ss14-ru"]
        self._cleanup_orphans(
            self.ru_ru_dir, valid_ru_files, ignore_dirs=ignore_dirs_ru
        )

        valid_robust_files = self._sync_directories(
            self.robust_en_dir, self.robust_ru_dir, global_memory
        )
        self._cleanup_orphans(self.robust_ru_dir, valid_robust_files)

    def _load_translation_memory(self) -> Dict[str, FtlEntry]:
        memory: Dict[str, FtlEntry] = {}
        for target_dir in (self.ru_ru_dir, self.robust_ru_dir):
            if not target_dir.exists():
                continue
            for ru_file in target_dir.rglob("*.ftl"):
                if self.context.is_ignored(ru_file):
                    continue
                entries, _, _ = self._get_parsed_ftl(ru_file)
                memory.update(entries)
        return memory

    @staticmethod
    def _enforce_inline_braces_spacing(text: str) -> str:
        if not text:
            return text

        def replacer(match: re.Match) -> str:
            content = match.group(1).strip()
            return f"{{ {content} }}"

        return re.sub(r"\{([^{}\n]+)\}", replacer, text)

    @staticmethod
    def _align_string_to_template(template: str, target: str) -> str:
        if not template or not target:
            return target

        target = re.sub(
            r"((?:^|\n)[ \t]*\*?\[[^\]\n]+\][^\n{]*?)[ \t]*\n[ \t]*(\{)",
            lambda m: f"{m.group(1).rstrip()} {m.group(2)}",
            target,
        )

        template_lines = template.split("\n")
        target_lines = target.split("\n")

        if len(template_lines) <= 1 and len(target_lines) <= 1:
            return target

        temp_has_text_on_first = bool(template_lines[0].strip())
        targ_has_text_on_first = bool(target_lines[0].strip())

        if temp_has_text_on_first and not targ_has_text_on_first:
            first_idx = next((i for i, l in enumerate(target_lines) if l.strip()), -1)
            if first_idx != -1:
                target_lines = target_lines[first_idx:]
                target_lines[0] = target_lines[0].lstrip()
        elif not temp_has_text_on_first and targ_has_text_on_first:
            target_lines.insert(0, "")

        is_inline = bool(target_lines[0].strip())
        formatted_lines = [target_lines[0].rstrip()] if is_inline else [""]

        has_selectors = "->" in template or "->" in target

        if not has_selectors:
            for i in range(1, len(target_lines)):
                stripped = target_lines[i].strip()

                if not stripped:
                    formatted_lines.append("")
                    continue

                temp_idx = i if i < len(template_lines) else len(template_lines) - 1

                leading_ws = ""
                for j in range(temp_idx, 0, -1):
                    if j < len(template_lines) and template_lines[j].strip():
                        leading_ws = template_lines[j][
                            : len(template_lines[j]) - len(template_lines[j].lstrip())
                        ]
                        break

                if not leading_ws:
                    leading_ws = "    "

                formatted_lines.append(leading_ws + stripped)

            return "\n".join(formatted_lines)

        depth = target_lines[0].count("->") if is_inline else 0

        for i in range(1, len(target_lines)):
            stripped = target_lines[i].strip()

            if not stripped:
                formatted_lines.append("")
                continue

            is_closing = stripped.startswith("}")
            closes_selector = 1 if is_closing else 0
            open_selectors = stripped.count("->")

            current_line_depth = max(0, depth - closes_selector)

            if stripped.startswith("[") or stripped.startswith("*["):
                if is_inline:
                    indent = 4 + max(0, current_line_depth - 1) * 8
                else:
                    indent = 8 + max(0, current_line_depth - 1) * 8
            elif is_closing:
                if is_inline:
                    indent = current_line_depth * 8
                else:
                    indent = 4 + current_line_depth * 8
            else:
                if is_inline:
                    indent = 8 + max(0, current_line_depth - 1) * 8
                    if current_line_depth == 0:
                        indent = 0
                else:
                    indent = 12 + max(0, current_line_depth - 1) * 8
                    if current_line_depth == 0:
                        indent = 4

            formatted_lines.append((" " * indent) + stripped)
            depth = depth - closes_selector + open_selectors

        return "\n".join(formatted_lines)

    @staticmethod
    def _sanitize_yaml_string(s: str, indent: str) -> str:
        if not s:
            return s
        lines = s.split("\n")
        res = [lines[0]]
        for line in lines[1:]:
            if line.strip() and not line.startswith(" ") and not line.startswith("\t"):
                res.append(indent + line)
            else:
                res.append(line)
        return "\n".join(res)

    @classmethod
    def _replace_dashes_in_text(cls, text: str) -> str:
        if not text:
            return text

        lines = text.split("\n")
        new_lines = []
        changed = False

        for line in lines:
            stripped = line.strip()
            if not stripped or stripped.startswith("-") or stripped.endswith("-"):
                new_lines.append(line)
                continue

            new_line = cls.RE_INNER_DASH.sub("—", line)
            new_lines.append(new_line)
            if new_line != line:
                changed = True

        return "\n".join(new_lines) if changed else text

    def _get_best_translation(
        self,
        key: str,
        en_entry: FtlEntry,
        ru_entries: Dict[str, FtlEntry],
        global_memory: Dict[str, FtlEntry],
        keep_ru_format: bool = False,
    ) -> Tuple[str, Dict[str, FtlAttribute]]:
        source_entry = ru_entries.get(key) or global_memory.get(key)

        if source_entry:
            attrs = {}
            for attr_name, en_attr in en_entry.attributes.items():
                if attr_name in source_entry.attributes:
                    ru_attr = source_entry.attributes[attr_name]
                    if keep_ru_format:
                        aligned_val = ru_attr.value
                    else:
                        aligned_val = self._align_string_to_template(
                            en_attr.value, ru_attr.value
                        )

                    aligned_val = self._enforce_inline_braces_spacing(aligned_val)

                    attrs[attr_name] = FtlAttribute(
                        en_attr.name,
                        aligned_val,
                        key_space=ru_attr.key_space
                        if keep_ru_format
                        else en_attr.key_space,
                        val_space=ru_attr.val_space
                        if keep_ru_format
                        else en_attr.val_space,
                        indent=ru_attr.indent if keep_ru_format else en_attr.indent,
                    )
                else:
                    attrs[attr_name] = FtlAttribute(
                        en_attr.name,
                        self._enforce_inline_braces_spacing(en_attr.value),
                        key_space=en_attr.key_space,
                        val_space=en_attr.val_space,
                        indent=en_attr.indent,
                    )

            if keep_ru_format:
                aligned_ru_val = source_entry.value
            else:
                aligned_ru_val = self._align_string_to_template(
                    en_entry.value, source_entry.value
                )

            aligned_ru_val = self._enforce_inline_braces_spacing(aligned_ru_val)

            return aligned_ru_val, attrs

        attrs = {
            k: FtlAttribute(
                v.name,
                self._enforce_inline_braces_spacing(v.value),
                v.key_space,
                v.val_space,
                v.indent,
            )
            for k, v in en_entry.attributes.items()
        }
        return self._enforce_inline_braces_spacing(en_entry.value), attrs

    def _sync_directories(
        self, src_dir: Path, target_dir: Path, global_memory: Dict[str, FtlEntry]
    ) -> Set[Path]:
        valid_targets: Set[Path] = set()

        if not src_dir.exists():
            logging.warning(f"Исходная директория {src_dir} не найдена.")
            return valid_targets

        for src_file in src_dir.rglob("*.ftl"):
            rel_path = src_file.relative_to(src_dir)
            target_file = target_dir / rel_path

            if self.context.is_ignored(target_file):
                continue

            valid_targets.add(target_file.resolve())

            en_entries, en_trailing, en_ends = self._get_parsed_ftl(src_file)
            ru_entries, ru_trailing, ru_ends = (
                self._get_parsed_ftl(target_file)
                if target_file.exists()
                else ({}, [], True)
            )

            is_new_file = not target_file.exists()
            new_ru_entries: Dict[str, FtlEntry] = {}
            updated = False

            fmt = self.format_level
            if fmt == "high" or (fmt == "low" and is_new_file):
                for key, en_entry in en_entries.items():
                    ru_val, ru_attrs = self._get_best_translation(
                        key, en_entry, ru_entries, global_memory, keep_ru_format=False
                    )

                    if (
                        key in ru_entries
                        and ru_entries[key].variables != en_entry.variables
                    ):
                        logging.warning(
                            f"Несовпадение переменных в {rel_path}: {key}. "
                            f"Ожидается: {en_entry.variables}, найдено: {ru_entries[key].variables}"
                        )

                    new_ru_entries[key] = FtlEntry(
                        key=key,
                        value=ru_val,
                        attributes=ru_attrs,
                        ignore_untranslated=en_entry.ignore_untranslated,
                        variables=set(en_entry.variables),
                        preceding_text=en_entry.preceding_text.copy(),
                        key_space=en_entry.key_space,
                        val_space=en_entry.val_space,
                    )
                trailing_text = en_trailing
                ends_with_newline = en_ends
                if (
                    list(ru_entries.keys()) != list(new_ru_entries.keys())
                    or not is_new_file
                ):
                    updated = True

            elif fmt == "medium" or (fmt == "low" and not is_new_file):
                for key, en_entry in en_entries.items():
                    ru_val, ru_attrs = self._get_best_translation(
                        key,
                        en_entry,
                        ru_entries,
                        global_memory,
                        keep_ru_format=(key in ru_entries),
                    )

                    if key in ru_entries:
                        prec_text = ru_entries[key].preceding_text
                        key_sp = ru_entries[key].key_space
                        val_sp = ru_entries[key].val_space
                        if ru_entries[key].variables != en_entry.variables:
                            logging.warning(
                                f"Несовпадение переменных в {rel_path}: {key}. "
                                f"Ожидается: {en_entry.variables}, найдено: {ru_entries[key].variables}"
                            )
                    else:
                        prec_text = en_entry.preceding_text.copy()
                        key_sp = en_entry.key_space
                        val_sp = en_entry.val_space

                    new_ru_entries[key] = FtlEntry(
                        key=key,
                        value=ru_val,
                        attributes=ru_attrs,
                        ignore_untranslated=en_entry.ignore_untranslated,
                        variables=set(en_entry.variables),
                        preceding_text=prec_text,
                        key_space=key_sp,
                        val_space=val_sp,
                    )
                trailing_text = ru_trailing if not is_new_file else en_trailing
                ends_with_newline = ru_ends if not is_new_file else en_ends
                if list(ru_entries.keys()) != list(new_ru_entries.keys()):
                    updated = True

            if updated or is_new_file:
                FtlParser.write_file(
                    target_file, new_ru_entries, trailing_text, ends_with_newline
                )

        return valid_targets

    def _cleanup_orphans(
        self,
        target_dir: Path,
        valid_targets: Set[Path],
        ignore_dirs: Optional[List[Path]] = None,
    ) -> None:
        if not target_dir.exists():
            return

        ignore_resolved = [p.resolve() for p in (ignore_dirs or [])]

        for target_file in target_dir.rglob("*.ftl"):
            resolved_file = target_file.resolve()

            if self.context.is_ignored(target_file):
                continue

            if any(resolved_file.is_relative_to(ignore) for ignore in ignore_resolved):
                continue

            if resolved_file not in valid_targets:
                logging.info(
                    f"Удаление устаревшего файла: {target_file.relative_to(self.root_dir)}"
                )
                try:
                    target_file.unlink()
                except OSError as e:
                    logging.error(f"Ошибка удаления файла {target_file}: {e}")

    def process_prototypes(self) -> None:
        logging.info("Обработка прототипов...")
        if not self.prototypes_src_dir.exists():
            logging.error(
                f"Директория прототипов не найдена: {self.prototypes_src_dir}"
            )
            return

        yml_files = list(self.prototypes_src_dir.rglob("*.yml"))
        prototypes: Dict[str, PrototypeEntry] = {}

        with ProcessPoolExecutor() as executor:
            futures = {
                executor.submit(self._parse_yaml, yml_file): yml_file
                for yml_file in yml_files
            }
            for future in as_completed(futures):
                try:
                    for proto in future.result():
                        prototypes[proto.id] = proto
                except Exception as e:
                    logging.error(f"Ошибка парсинга YAML в {futures[future]}: {e}")

        resolved_prototypes = self._resolve_inheritance(prototypes)
        valid_ftls = self._generate_prototype_ftls(resolved_prototypes)

        self._cleanup_orphans(self.prototypes_ru_dir, valid_ftls)

        logging.info("Обработка прототипов завершена.")

    @staticmethod
    def _parse_yaml(filepath: Path) -> List[PrototypeEntry]:
        entries: List[PrototypeEntry] = []
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                documents = list(yaml.load_all(f, Loader=SS14YamlLoader))
                idx = 0
                for doc in documents:
                    if isinstance(doc, list):
                        items_to_check = doc
                    elif isinstance(doc, dict):
                        items_to_check = [doc]
                    else:
                        continue

                    for item in items_to_check:
                        if (
                            not isinstance(item, dict)
                            or item.get("type") != "entity"
                            or "id" not in item
                        ):
                            continue

                        raw_id = item["id"]
                        raw_name = item.get("name")
                        raw_desc = item.get("description") or item.get("desc")
                        raw_suffix = item.get("suffix")

                        parent_raw = item.get("parent") or item.get("parents", [])
                        if isinstance(parent_raw, list):
                            parents = [str(p) for p in parent_raw]
                        elif parent_raw is not None:
                            parents = [str(parent_raw)]
                        else:
                            parents = []

                        entries.append(
                            PrototypeEntry(
                                id=str(raw_id),
                                parents=parents,
                                name=str(raw_name) if raw_name is not None else None,
                                desc=str(raw_desc) if raw_desc is not None else None,
                                suffix=str(raw_suffix)
                                if raw_suffix is not None
                                else None,
                                resolved_name=str(raw_name)
                                if raw_name is not None
                                else None,
                                resolved_desc=str(raw_desc)
                                if raw_desc is not None
                                else None,
                                resolved_suffix=str(raw_suffix)
                                if raw_suffix is not None
                                else None,
                                filepath=str(filepath),
                                file_index=idx,
                            )
                        )
                        idx += 1
        except Exception:
            pass
        return entries

    def _resolve_inheritance(
        self, prototypes: Dict[str, PrototypeEntry]
    ) -> Dict[str, PrototypeEntry]:
        resolved: Dict[str, PrototypeEntry] = {}

        def resolve(proto_id: str, visited: Set[str]) -> PrototypeEntry:
            if proto_id in resolved:
                return resolved[proto_id]

            proto = prototypes.get(proto_id, PrototypeEntry(id=proto_id, parents=[]))

            if proto_id in visited:
                return proto

            visited.add(proto_id)
            final_name, final_desc, final_suffix = proto.name, proto.desc, proto.suffix

            name_parent = None
            desc_parent = None
            suffix_parent = None

            for parent_id in proto.parents:
                parent_proto = resolve(parent_id, visited)

                if final_name is None and parent_proto.resolved_name is not None:
                    final_name = parent_proto.resolved_name
                    name_parent = parent_id

                if final_desc is None and parent_proto.resolved_desc is not None:
                    final_desc = parent_proto.resolved_desc
                    desc_parent = parent_id

                if final_suffix is None and parent_proto.resolved_suffix is not None:
                    final_suffix = parent_proto.resolved_suffix
                    suffix_parent = parent_id

            resolved_proto = PrototypeEntry(
                id=proto.id,
                parents=proto.parents,
                name=proto.name,
                desc=proto.desc,
                suffix=proto.suffix,
                name_parent=name_parent,
                desc_parent=desc_parent,
                suffix_parent=suffix_parent,
                resolved_name=final_name,
                resolved_desc=final_desc,
                resolved_suffix=final_suffix,
                filepath=proto.filepath,
                file_index=proto.file_index,
            )
            resolved[proto_id] = resolved_proto
            visited.remove(proto_id)
            return resolved_proto

        for p_id in prototypes:
            resolve(p_id, set())

        return resolved

    def _generate_prototype_ftls(
        self, prototypes: Dict[str, PrototypeEntry]
    ) -> Set[Path]:
        external_keys: Set[str] = set()

        if self.ru_ru_dir.exists():
            for ftl_file in self.ru_ru_dir.rglob("*.ftl"):
                if ftl_file.is_relative_to(self.prototypes_ru_dir):
                    continue
                if self.context.is_ignored(ftl_file):
                    continue
                entries, _, _ = self._get_parsed_ftl(ftl_file)
                external_keys.update(entries.keys())

        existing_files: Dict[Path, Tuple[Dict[str, FtlEntry], List[str], bool]] = {}
        global_entries: Dict[str, Tuple[FtlEntry, Path]] = {}

        if self.prototypes_ru_dir.exists():
            for ftl_file in self.prototypes_ru_dir.rglob("*.ftl"):
                if self.context.is_ignored(ftl_file):
                    continue
                entries, trailing, ends = self._get_parsed_ftl(ftl_file)
                existing_files[ftl_file] = (entries, trailing, ends)
                for k, v in entries.items():
                    global_entries[k] = (v, ftl_file)

        desired_layout: Dict[Path, List[Tuple[PrototypeEntry, FtlEntry]]] = {}
        used_keys = set()

        for proto in prototypes.values():
            ftl_key = f"ent-{proto.id}"

            if ftl_key in external_keys:
                continue

            try:
                rel_path = Path(proto.filepath).relative_to(self.prototypes_src_dir)
                lower_parts = [p.lower() for p in rel_path.parts]
                ftl_path = self.prototypes_ru_dir.joinpath(*lower_parts).with_suffix(
                    ".ftl"
                )
            except ValueError:
                continue

            if self.context.is_ignored(ftl_path):
                continue

            used_keys.add(ftl_key)
            mem_entry = (
                global_entries.get(ftl_key)[0] if ftl_key in global_entries else None
            )

            entry = FtlEntry(key=ftl_key, value="")

            if proto.name is not None:
                entry.value = self._sanitize_yaml_string(str(proto.name), "    ")
            elif proto.name_parent:
                entry.value = f"{{ ent-{proto.name_parent} }}"
            elif proto.parents:
                entry.value = f"{{ ent-{proto.parents[0]} }}"
            else:
                entry.value = '{ "" }'

            if proto.desc is not None:
                safe_desc = self._sanitize_yaml_string(str(proto.desc), "        ")
                entry.attributes["desc"] = FtlAttribute("desc", safe_desc)
            elif proto.desc_parent:
                entry.attributes["desc"] = FtlAttribute(
                    "desc", f"{{ ent-{proto.desc_parent}.desc }}"
                )
            elif proto.parents:
                entry.attributes["desc"] = FtlAttribute(
                    "desc", f"{{ ent-{proto.parents[0]}.desc }}"
                )
            else:
                entry.attributes["desc"] = FtlAttribute("desc", '{ "" }')

            if proto.suffix is not None:
                safe_suffix = self._sanitize_yaml_string(str(proto.suffix), "        ")
                entry.attributes["suffix"] = FtlAttribute("suffix", safe_suffix)
            elif proto.suffix_parent:
                entry.attributes["suffix"] = FtlAttribute(
                    "suffix", f"{{ ent-{proto.suffix_parent}.suffix }}"
                )

            if mem_entry:
                if proto.name is not None:
                    if mem_entry.value.strip() and not mem_entry.value.startswith("{"):
                        entry.value = mem_entry.value
                        entry.key_space = mem_entry.key_space
                        entry.val_space = mem_entry.val_space

                for attr_name, mem_attr in mem_entry.attributes.items():
                    if not mem_attr.value.strip():
                        continue

                    if attr_name == "desc" and proto.desc is None:
                        continue
                    if attr_name == "suffix" and proto.suffix is None:
                        continue

                    if attr_name in entry.attributes:
                        if not mem_attr.value.startswith("{"):
                            entry.attributes[attr_name].value = mem_attr.value
                            entry.attributes[attr_name].key_space = mem_attr.key_space
                            entry.attributes[attr_name].val_space = mem_attr.val_space
                            entry.attributes[attr_name].indent = mem_attr.indent
                    else:
                        if not mem_attr.value.startswith("{"):
                            entry.attributes[attr_name] = FtlAttribute(
                                attr_name,
                                mem_attr.value,
                                mem_attr.key_space,
                                mem_attr.val_space,
                                mem_attr.indent,
                            )

            if ftl_path not in desired_layout:
                desired_layout[ftl_path] = []
            desired_layout[ftl_path].append((proto, entry))

        files_to_write: Dict[Path, Dict[str, FtlEntry]] = {}
        valid_ftls = set()

        for ftl_path, items in desired_layout.items():
            valid_ftls.add(ftl_path)
            items.sort(key=lambda x: (x[0].filepath, x[0].file_index))

            file_entries = {}
            for proto, entry in items:
                file_entries[entry.key] = entry
            files_to_write[ftl_path] = file_entries

        for key, (entry, old_path) in global_entries.items():
            if key not in used_keys and key not in external_keys:
                valid_ftls.add(old_path)
                if old_path not in files_to_write:
                    files_to_write[old_path] = {}
                files_to_write[old_path][key] = entry

        for ftl_path, entries_dict in files_to_write.items():
            if not entries_dict:
                if ftl_path.exists():
                    try:
                        ftl_path.unlink()
                    except OSError:
                        pass
                valid_ftls.discard(ftl_path)
                continue

            if ftl_path in existing_files:
                trailing = existing_files[ftl_path][1]
                ends = existing_files[ftl_path][2]
            else:
                trailing = []
                ends = True

            FtlParser.write_file(ftl_path, entries_dict, trailing, ends)

        return valid_ftls

    def fix_dashes(self) -> None:
        logging.info("Расстановка правильных тире вместо дефисов в локализации...")
        changed_files = 0
        search_dirs = [self.ru_ru_dir, self.robust_ru_dir]

        for directory in search_dirs:
            if not directory.exists():
                continue

            for ftl_file in directory.rglob("*.ftl"):
                if self.context.is_ignored(ftl_file):
                    continue

                entries, trailing, ends = FtlParser.parse_file(ftl_file)
                file_changed = False

                for entry in entries.values():
                    new_val = self._replace_dashes_in_text(entry.value)
                    if new_val != entry.value:
                        entry.value = new_val
                        file_changed = True

                    for attr in entry.attributes.values():
                        new_attr_val = self._replace_dashes_in_text(attr.value)
                        if new_attr_val != attr.value:
                            attr.value = new_attr_val
                            file_changed = True

                if file_changed:
                    FtlParser.write_file(ftl_file, entries, trailing, ends)
                    changed_files += 1
                    logging.info(
                        f"Обновлены тире в файле: {ftl_file.relative_to(self.root_dir)}"
                    )

        logging.info(f"Завершена расстановка тире. Изменено файлов: {changed_files}")

    def check_untranslated(self) -> None:
        logging.info("Поиск непереведенных строк...")
        untranslated_count = 0

        search_dirs = [self.ru_ru_dir, self.robust_ru_dir]

        for directory in search_dirs:
            if not directory.exists():
                continue

            for ftl_file in directory.rglob("*.ftl"):
                if self.context.is_ignored(ftl_file):
                    continue

                if self.context.is_untranslated_ignored(ftl_file):
                    continue

                entries, _, _ = self._get_parsed_ftl(ftl_file)
                for key, entry in entries.items():
                    if entry.ignore_untranslated:
                        continue

                    def is_untranslated(text: str) -> bool:
                        if not text:
                            return False
                        clean_text = FtlParser.TAG_REGEX.sub(
                            "", FtlParser.REF_REGEX.sub("", text)
                        )
                        if not clean_text.strip():
                            return False
                        return bool(
                            self.LATIN_PATTERN.search(clean_text)
                            and not self.CYRILLIC_PATTERN.search(clean_text)
                        )

                    if is_untranslated(entry.value) or any(
                        is_untranslated(attr.value)
                        for attr in entry.attributes.values()
                    ):
                        logging.warning(
                            f"Возможно не переведено: {ftl_file.relative_to(self.root_dir)} -> {key}"
                        )
                        untranslated_count += 1

        logging.info(f"Найдено потенциально непереведенных строк: {untranslated_count}")


def setup_logging() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s[%(levelname)s] %(message)s",
        datefmt="%H:%M:%S",
    )


def main() -> None:
    setup_logging()

    script_dir = Path(__file__).resolve().parent
    default_root = script_dir.parent.parent

    parser = argparse.ArgumentParser(
        description="Инструмент управления локализацией SS14."
    )
    parser.add_argument(
        "--root",
        type=str,
        default=str(default_root),
        help="Путь к корню проекта (по умолчанию автоматически определяется корень репозитория).",
    )
    parser.add_argument(
        "--format",
        type=str,
        choices=["low", "medium", "high"],
        default="high",
        help="Уровень сохранения форматирования (low, medium, high). По умолчанию: high.",
    )
    parser.add_argument(
        "--config",
        type=str,
        default="localize_config.yml",
        help="Имя конфигурационного файла (по умолчанию localize_config.yml).",
    )
    parser.add_argument(
        "--sync", action="store_true", help="Синхронизировать системные FTL файлы."
    )
    parser.add_argument(
        "--prototypes",
        action="store_true",
        help="Сгенерировать локализацию из YAML прототипов.",
    )
    parser.add_argument(
        "--dashes",
        action="store_true",
        help="Расставить тире вместо дефисов в локализации.",
    )
    parser.add_argument(
        "--check", action="store_true", help="Проверить непереведенные строки."
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Выполнить все операции (используется по умолчанию, если ничего не передано).",
    )

    args = parser.parse_args()

    if not any([args.sync, args.prototypes, args.check, args.dashes, args.all]):
        args.all = True

    root_path = Path(args.root).resolve()
    manager = LocalizationManager(root_path, args.format, args.config)

    try:
        if args.sync or args.all:
            manager.sync_systems()
        if args.prototypes or args.all:
            manager.process_prototypes()
        if args.dashes or args.all:
            manager.fix_dashes()
        if args.check or args.all:
            manager.check_untranslated()

    except KeyboardInterrupt:
        logging.info("Операция прервана пользователем.")


if __name__ == "__main__":
    multiprocessing.freeze_support()
    main()
