# Vss2Git.Cli

Command-line interface for Vss2Git. Supports scripted and unattended migrations, VSS database browsing, and post-migration verification.

## Commands

The CLI uses verbs to select what to do. If no verb is given, `migrate` is assumed.

### migrate

Runs a full VSS-to-Git migration.

```
Vss2Git.Cli migrate --vss-dir <path> --git-dir <path> [options]
```

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--vss-dir` | `-v` | *(required)* | Path to VSS database (the folder with `srcsafe.ini`) |
| `--git-dir` | `-g` | *(required)* | Output directory for the Git repository |
| `--vss-project` | `-p` | `$` | VSS project path to export (e.g. `$/MyApp/Server`) |
| `--exclude` | `-e` | | Exclude patterns, semicolon-separated (`*.exe;docs/**`) |
| `--email-domain` | `-d` | `localhost` | Domain for generated commit emails |
| `--default-comment` | | *(empty)* | Fallback comment for changesets with no VSS comment |
| `--encoding` | `-c` | system default | VSS encoding code page (e.g. `1251`, `1252`) |
| `--log` | `-l` | `Vss2Git.log` | Log file path |
| `--ignore-errors` | `-i` | `false` | Skip errors instead of aborting |
| `--force` | `-f` | `false` | Proceed even if the output directory is not empty |
| `--interactive` | | `false` | Prompt on errors (Abort/Retry/Ignore) |
| `--git-backend` | | `Process` | `Process`, `LibGit2Sharp`, or `FastImport` |
| `--from-date` | | | Start exporting from this date (`yyyy-MM-dd`). Earlier revisions still build internal state. |
| `--to-date` | | | Stop after the last changeset on or before this date |
| `--transcode` | `-t` | `true` | Convert comments to UTF-8. Set `false` to keep original encoding. |
| `--force-annotated-tags` | | `true` | Use annotated tags for VSS labels |
| `--export-to-root` | | `false` | Place project contents at Git root instead of in a subfolder |
| `--any-comment-threshold` | | `0` | Max seconds between revisions with different comments to group them |
| `--same-comment-threshold` | | `60` | Max seconds between revisions with the same comment to group them |
| `--perf` | | `false` | Print performance breakdown at the end |

### list

Lists items in a VSS database — projects, files, or shared files. Handy for exploring a database before deciding what to migrate.

```
Vss2Git.Cli list --vss-dir <path> [options]
```

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--vss-dir` | `-v` | *(required)* | Path to VSS database |
| `--vss-project` | `-p` | `$` | Starting project (e.g. `$/MyApp/Server`) |
| `--encoding` | `-c` | system default | VSS encoding code page |
| `--type` | `-t` | `all` | What to list: `projects`, `files`, or `all` |
| `--shared` | `-s` | `false` | Only shared files (referenced from multiple projects); output is grouped by physical file |
| `--include-deleted` | | `false` | Include soft-deleted entries (default: hidden, matching VSS GUI). In `--shared` output, also reveals files flagged Shared but with only one live reference (usually noise — sharing across the scan boundary or with a since-deleted twin). Destroyed items are gone regardless. |
| `--format` | `-f` | `tree` | Output format: `tree` or `flat`. Ignored when `--shared` is set. |

Example tree output (`--type projects`):

```
$/MyApp/
├── Client/
├── Common/
│   └── Utils/
├── Server/
│   └── Migrations/
├── Tools/
└── Web/
    ├── api/
    └── public/

8 projects, 0 files
```

Example shared-files output (`--shared`):

```
Shared files in $ — 12 files, 27 references
═══════════════════════════════════════════

libcrypto-1_1.dll  [OJWCAAAA]
  $/ThirdParty/OpenSSL/
  $/MyApp/ServiceA/Exe/
  $/MyApp/ServiceB/Exe/
...
```

Sorted by fanout (most-shared first), then alphabetically. Filenames are bold in interactive terminals (auto-disabled when output is redirected, or when `NO_COLOR` is set). `[OJWCAAAA]` is the VSS physical id; paths show the directory only — the filename is constant across the group.

### verify

Compares two directories and reports missing or extra files. Useful for validating migration output against a VSS working copy.

```
Vss2Git.Cli verify -s <source> -t <target> [-x <patterns>]
```

| Option | Short | Description |
|--------|-------|-------------|
| `--source` | `-s` | Source directory (e.g. VSS working folder) |
| `--target` | `-t` | Target directory (e.g. Git output) |
| `--exclude` | `-x` | Semicolon-separated patterns to ignore (`.vs;.git;bin`) |

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Bad arguments or startup error |
| 2 | Migration completed with errors |
