# Git Fast-Import Format Specification

**Authoritative Sources:**
- https://git-scm.com/docs/git-fast-import
- https://www.kernel.org/pub/software/scm/git/docs/git-fast-import.html

## Overview

Git fast-import is a backend tool for bulk-importing data into Git repositories. It reads a stream of text commands from stdin and writes objects directly to the Git database.

The format is text-based, which makes it easy to debug and work with programmatically. However, fast-import is extremely strict about the input format - even a single extra space or wrong line ending will cause it to abort immediately. It uses marks (like `:1`, `:2`) to reference objects without needing to know their SHA-1 hashes ahead of time, and can handle repositories of any size by streaming data incrementally.

## Strictness Requirements (READ THIS!)

Fast-import will crash if you get the format even slightly wrong. When the docs say:

- **SP** - this means exactly one space (0x20), not two, not a tab
- **LF** - this means exactly one linefeed (0x0A), Unix-style only
- **HT** - this means exactly one tab (0x09)

No extra whitespace anywhere. No Windows CRLF line endings. Get it wrong and you'll find a crash report in `.git/fast_import_crash_*`.

## Command Overview

A fast-import stream consists of a sequence of commands, one per line:

| Command | Purpose |
|---------|---------|
| `blob` | Create a file blob |
| `commit` | Create or update a branch with a new commit |
| `tag` | Create an annotated tag |
| `reset` | Reset a branch or create/delete refs |
| `checkpoint` | Flush Git object database to disk |
| `progress` | Print a message to stdout |
| `feature` | Request/declare features |
| `option` | Set import options |
| `done` | Terminate stream (required with `--done` flag) |

## Comments

Lines starting with `#` are ignored:

```
# This is a comment
```

Watch out though - if you put a `#` line inside a `data` block, it becomes part of the actual data, not a comment!

---

## Data Command

The `data` command is how you supply raw content - commit messages, tag messages, and file blobs.

### Byte Count Format (use this one)

```
'data' SP <count> LF
<raw>
LF?
```

Example:
```
data 14
Hello, world!

```

The `<count>` is the exact number of bytes that follow (not counting the newline after the count itself). You can optionally add a final LF after the data. Commit and tag messages must be UTF-8, but file content can be any encoding.

### Delimited Format

```
'data' SP '<<' <delim> LF
<raw>
<delim> LF
```

Example:
```
data <<EOF
This is a commit message
with multiple lines.
EOF

```

This looks nicer for testing, but the byte count format is what you want for production code.

---

## Commit Command

This creates a new commit (or updates an existing branch).

### Full Syntax

```
'commit' SP <ref> LF
mark?
original-oid?
('author' (SP <name>)? SP LT <email> GT SP <when> LF)?
'committer' (SP <name>)? SP LT <email> GT SP <when> LF
('gpgsig' SP <algo> SP <format> LF data)?
('encoding' SP <encoding> LF)?
data
('from' SP <commit-ish> LF)?
('merge' SP <commit-ish> LF)*
(filemodify | filedelete | filecopy | filerename | filedeleteall | notemodify)*
LF?
```

### Minimal Example

```
commit refs/heads/master
mark :1
committer John Doe <john@example.com> 1234567890 +0000
data 21
Initial commit here

```

### Complete Example with Parent

```
commit refs/heads/master
mark :2
author Jane Smith <jane@example.com> 1234567891 +0000
committer Jane Smith <jane@example.com> 1234567891 +0000
data 27
Add feature X to the code

from :1
M 100644 inline src/main.c
data 42
int main() {
    return 0;
}

```

### Field Details

#### `<ref>` - Branch Reference

Must be a valid Git refname like `refs/heads/master` or `refs/heads/feature-branch`.

#### `mark` - Object Reference (Optional)

```
mark :123
```

This assigns a mark number to the commit so you can refer to it later as `:123`. Mark numbers start at 1 (0 is reserved). You can reuse mark numbers if you want to reassign them to different objects. Marks let you reference commits without knowing their SHA-1 hashes.

#### `author` - Original Author (Optional)

```
author Full Name <email@example.com> <timestamp> <timezone>
```

The timestamp is seconds since Unix epoch (1970-01-01 00:00:00 UTC) and timezone is `+HHMM` or `-HHMM`. For example: `author John Doe <john@example.com> 1609459200 +0000`

If you skip this, git treats the committer as the author.

#### `committer` - Committer (REQUIRED)

```
committer Full Name <email@example.com> <timestamp> <timezone>
```

This is the only mandatory identity field - you must include it.

#### `encoding` - Commit Message Encoding (Optional)

```
encoding ISO-8859-1
```

This exists for importing legacy commits with weird encodings, but just use UTF-8 for everything. Really.

#### `data` - Commit Message (REQUIRED)

The commit message goes right after the committer line:

```
data 45
Fix the null pointer bug in authentication

```

Must be UTF-8. The byte count must be exact (including the LF at the end if you have one).

#### `from` - Parent Commit (Optional)

```
from :1
```

You can also use `from refs/heads/develop` or `from a1b2c3d4e5f6...` (full SHA-1).

If you leave this out:
- On a new branch, you get a root commit (no parent)
- On an existing branch, it uses the current tip as the parent

In practice, you almost always want to specify `from` unless you're intentionally creating a root commit.

#### `merge` - Additional Parents (Optional, Multiple)

```
merge :5
merge :7
```

For merge commits with multiple parents. The first parent comes from `from`, and you add more with `merge` lines.

Example three-way merge:
```
commit refs/heads/master
mark :10
committer Merger <merge@example.com> 1234567900 +0000
data 25
Merge feature branches

from :8
merge :9
merge :10
```

---

## File Modification Commands

These specify what changes to make to files in the commit.

### M - Modify/Add File

Adds or modifies a file. You can either reference a blob you created earlier:

```
M <mode> <dataref> <path>
```

Example:
```
M 100644 :45 src/config.ini
```

Or include the data inline:

```
M <mode> inline <path>
data <count>
<raw>
```

Example:
```
M 100644 inline README.txt
data 28
This is the README file.

```

#### File Modes

| Mode | What it means |
|------|---------------|
| `100644` or `644` | Regular file (not executable) |
| `100755` or `755` | Executable file |
| `120000` | Symbolic link |
| `160000` | Git submodule |
| `040000` | Subdirectory (tree object) |

For normal files, use `100644` - that's what you'll use 99% of the time.

### D - Delete File/Directory

```
D <path>
```

Deletes a file or recursively deletes a directory.

Example:
```
D old/deprecated/code.c
D old/deprecated
```

### R - Rename File/Directory

```
R <source-path> <dest-path>
```

Renames or moves a file or directory.

Example:
```
R src/old_name.c src/new_name.c
```

### C - Copy File/Directory

```
C <source-path> <dest-path>
```

Copies a file or directory.

Example:
```
C src/template.c src/variant.c
```

### deleteall - Clear Everything

```
deleteall
```

Nukes all files in the tree. Useful when you're doing a complete rewrite.

Example:
```
commit refs/heads/master
mark :10
committer User <user@example.com> 1234567900 +0000
data 18
Complete rewrite

from :9
deleteall
M 100644 inline newfile.txt
data 5
new

```

---

## Tag Command

Creates an annotated tag.

### Syntax

```
'tag' SP <name> LF
mark?
'from' SP <commit-ish> LF
original-oid?
'tagger' (SP <name>)? SP LT <email> GT SP <when> LF
data
```

### Example

```
tag v1.0
from :100
tagger Release Manager <release@example.com> 1234567950 +0000
data 35
Version 1.0 - Initial release

```

The tag name automatically gets `refs/tags/` prepended. Tag messages must be UTF-8. Both `from` (which commit to tag) and `tagger` are required.

---

## Reset Command

Resets a branch or creates/deletes refs.

### Syntax

```
'reset' SP <ref> LF
('from' SP <commit-ish> LF)?
```

### Examples

Create a branch:
```
reset refs/heads/new-branch
from :50
```

Delete a branch:
```
reset refs/heads/old-branch
from 0000000000000000000000000000000000000000
```

Reset branch to specific commit:
```
reset refs/heads/master
from :100
```

---

## Checkpoint Command

```
checkpoint
```

Forces git to flush everything in memory to disk. This creates a recovery point and can help with memory pressure, but it slows things down a lot. Use sparingly.

---

## Progress Command

```
progress <text>
```

Prints a message to stdout. Useful for logging import progress.

Example:
```
progress Importing revision 1000 of 5000
```

---

## Feature Command

```
feature <feature-name> (= <argument>)?
```

Requests or declares feature support.

### Common Features

Require `done` at end:
```
feature done
```

Use raw date format (this is the default anyway):
```
feature date-format=raw
```

Load marks from a file:
```
feature import-marks=/path/to/marks
```

---

## Done Command

```
done
```

Signals the end of the stream. Required if you use `--done` flag or `feature done`. It's good practice to always include it.

---

## Path Rules

Paths must follow these rules:

Valid:
- `src/main.c`
- `docs/README.txt`
- `path/to/file.dat`

Invalid:
- `/absolute/path` - no leading slash
- `foo//bar` - no empty components
- `dir/` - no trailing slash
- `.` or `..` - no relative references
- Anything with NUL bytes

Always use forward slashes, even on Windows.

---

## Encoding Summary

| Component | Encoding |
|-----------|----------|
| Command syntax | ASCII |
| Commit messages | UTF-8 (required) |
| Tag messages | UTF-8 (required) |
| Author/committer names | UTF-8 (best practice) |
| File paths | UTF-8 (best practice) |
| File content (blobs) | Whatever you want |

---

## Error Handling

When fast-import hits an error, it immediately exits with a non-zero code and writes a crash report to `.git/fast_import_crash_*`. The crash report shows where in the stream things went wrong, what commit was being processed, and the state of the mark and branch tables.

Always check the exit code and read stderr for errors.

---

## Performance Tips

### 1. Use Marks Instead of SHA-1s

Bad:
```
from a1b2c3d4e5f6789...
```

Good:
```
mark :1
...
from :1
```

### 2. Flush Strategically

Flush after every commit if you need stability, or flush every N commits for better performance. Use `checkpoint` for recovery points in long imports.

### 3. Batch File Operations

Put all file operations for a commit together instead of spreading them out.

### 4. Inline vs Blob Command

For small files (< 1MB), use inline data. For larger files, consider using the `blob` command with marks.

### 5. Avoid deleteall

Incremental changes are faster than nuking everything and starting over.

---

## Complete Working Example

Here's a complete fast-import stream that actually works:

```
# Initialize with feature
feature done

# First commit (root)
commit refs/heads/master
mark :1
committer Alice <alice@example.com> 1609459200 +0000
data 14
Initial commit

M 100644 inline README.md
data 19
# My Project

```

# Second commit (with parent)
commit refs/heads/master
mark :2
author Bob <bob@example.com> 1609459260 +0000
committer Bob <bob@example.com> 1609459260 +0000
data 16
Add source file

from :1
M 100644 inline src/main.c
data 42
int main() {
    return 0;
}


# Third commit (modify existing file)
commit refs/heads/master
mark :3
committer Alice <alice@example.com> 1609459320 +0000
data 22
Update README

from :2
M 100644 inline README.md
data 38
# My Project

Now with more content!


# Create a tag
tag v1.0.0
from :3
tagger Release Manager <release@example.com> 1609459380 +0000
data 20
Version 1.0 release


# Signal completion
done
```

---

## Common Mistakes

### 1. Wrong Line Endings

Don't use CRLF:
```csharp
writer.WriteLine("commit refs/heads/master\r\n");  // NO!
```

Use LF:
```csharp
var bytes = Encoding.UTF8.GetBytes("commit refs/heads/master\n");
stream.Write(bytes, 0, bytes.Length);
```

### 2. Wrong Encoding

Don't use system default:
```csharp
var bytes = Encoding.Default.GetBytes(commitMessage);  // NO!
```

Use UTF-8:
```csharp
var bytes = Encoding.UTF8.GetBytes(commitMessage);
```

### 3. Counting Characters Instead of Bytes

Wrong:
```csharp
var message = "Hello, 世界";
writer.WriteLine($"data {message.Length}");  // Counts characters!
```

Right:
```csharp
var bytes = Encoding.UTF8.GetBytes(message);
writer.WriteLine($"data {bytes.Length}");
stream.Write(bytes, 0, bytes.Length);
```

### 4. Forgetting the Parent

Wrong:
```csharp
WriteLine("commit refs/heads/master");
WriteLine("mark :2");
// ... missing "from :1" ...
```

Right:
```csharp
WriteLine("commit refs/heads/master");
WriteLine("mark :2");
// ... committer, data ...
WriteLine("from :1");
```

### 5. Not Flushing

Wrong:
```csharp
var writer = new StreamWriter(process.StandardInput.BaseStream);
writer.WriteLine("commit refs/heads/master");
// Sits in buffer forever
```

Right:
```csharp
var stream = process.StandardInput.BaseStream;
var bytes = Encoding.UTF8.GetBytes("commit refs/heads/master\n");
stream.Write(bytes, 0, bytes.Length);
stream.Flush();
```

### 6. Backslashes in Paths

Wrong:
```csharp
WriteLine($"M 100644 inline src\\main.c");
```

Right:
```csharp
var path = filePath.Replace('\\', '/');
WriteLine($"M 100644 inline {path}");
```

---

## Testing

### Test 1: Minimal Import

```bash
git init test-repo
cd test-repo
git fast-import <<EOF
commit refs/heads/master
committer Test <test@example.com> 1234567890 +0000
data 5
Test

done
EOF

git log  # Should show one commit
```

### Test 2: Binary Data

Make sure your data command can handle binary files, not just text.

### Test 3: Unicode

Test with Unicode in paths and commit messages.

### Test 4: Large Files

Try files over 1MB to make sure the data command doesn't choke.

### Test 5: Multiple Branches

Create a few branches and make sure the references work.

---

## Debugging

1. Use `--stats` to see import statistics:
   ```bash
   git fast-import --stats < import.txt
   ```

2. Check `.git/fast_import_crash_*` when things go wrong

3. Validate by round-tripping through fast-export:
   ```bash
   git fast-export --all | git fast-import --force
   ```

4. Add `progress` commands to track what's happening

5. Test with marks files to make sure incremental imports work