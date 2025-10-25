# Visual SourceSafe Command Line Reference

This document defines the Visual SourceSafe command line commands and options.

---

## Commands

## About

Displays information about your copy of Visual SourceSafe.

Displays the version number and copyright, legal, and licensing notices.

**Project Rights:** Any Visual SourceSafe user can use this command.

---

## Add

Adds new files to the Visual SourceSafe database.

**Syntax:**
```
ss Add <local files> [-B] [-C] [-D-] [-H] [-I-] [-K] [-N] [-O] [-R] [-W] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- You can specify a file on any drive and in any directory, and add that file to the current Visual SourceSafe project
- To add a folder recursively (all subfolders and files), specify a folder instead of a file name and use the -R option
- Supports Universal Naming Convention (UNC) names (e.g., `\\COMPUTER\SHARE\FILE.TXT`)
- When specifying `*.*` or using -R option, the Add command checks the Relevant_Masks initialization variable

**Examples:**
```
# Adds Hello.c from c:\ directory to current project
ss Add C:\HELLO.C

# Adds multiple files from current Windows folder
ss Add TEST.C "My long filename.H"

# Adds all files in current folder
ss Add *

# Adds all files in current folder and subfolders recursively
ss Add *.* -R
```

---

## Branch

Breaks a share link and creates a branch of a file in a project.

**Syntax:**
```
ss Branch <file> [-C] [-H] [-I-] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- The branched file in the current project becomes independent of the file in all other projects
- Branched files can be modified independently
- Use the Paths command to show all share links for branched files

---

## Checkin

Updates the database with changes made to a checked out file and unlocks the master copy.

**Syntax:**
```
ss Checkin <items> [-C] [-G-] [-H] [-I-] [-K] [-N] [-O-] [-R] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- You must check out a file before you can check it in
- After checking in, Visual SourceSafe compares the modified file with the master copy and updates the database with any changes
- The working file is made read-only unless the Keep_Checked_Out option is used

**Examples:**
```
# Check in TEST.C with comment
ss Checkin TEST.C -C"Fixed memory leak"

# Check in all files recursively
ss Checkin *.* -R
```

---

## Checkout

Checks out a file and makes the working copy writable.

**Syntax:**
```
ss Checkout <items> [-C] [-G] [-GF] [-GL] [-GS] [-GT] [-GTM] [-GTU] [-H] [-I-] [-N] [-O-] [-R] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Checking out a file locks it in the database and makes your working copy writable
- Other users can see the file is checked out and by whom
- Multiple checkouts are possible if enabled in Visual SourceSafe settings

**Examples:**
```
# Check out TEST.C
ss Checkout TEST.C

# Check out with comment
ss Checkout TEST.C -C"Adding new feature"

# Check out all files recursively
ss Checkout *.* -R
```

---

## Cloak

Hides a project from recursive Get, Check Out, Check In, Undo Check Out, and Project Differences commands.

**Syntax:**
```
ss Cloak <project> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Cloaking is useful for excluding large or irrelevant subprojects from recursive operations
- Use Decloak to remove the cloaked attribute

---

## Comment

Changes the previously entered comment for a specific version of a file or project.

**Syntax:**
```
ss Comment <items> [-C] [-H] [-I-] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- You can change comments for any version of a file or project
- Use the -V option to specify which version's comment to change

---

## Copy

Copies one or more files from one project to another project.

**Syntax:**
```
ss Copy <project/file> <destination> [-C] [-E] [-H] [-I-] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right in the destination project.

**Notes:**
- The file is copied, not shared (no link is created)
- History is not copied with the file
- Use Share command if you want to create a link between projects

---

## CP

Sets the current project path.

**Syntax:**
```
ss CP <project> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Changes the current project for subsequent commands
- Use `ss Project` to display the current project path

**Example:**
```
# Set current project to $/MyProject
ss CP $/MyProject
```

---

## Create

Creates a new subproject.

**Syntax:**
```
ss Create <project> [-C] [-H] [-I-] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- Creates a new subproject under the current project
- The new project is empty initially

**Example:**
```
# Create a new subproject called NewFeature
ss Create $/MyProject/NewFeature
```

---

## Decloak

Removes the cloaked attribute from a project.

**Syntax:**
```
ss Decloak <project> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

---

## Delete

Removes files and projects from the database and marks them as deleted.

**Syntax:**
```
ss Delete <items> [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- Deleted items can be recovered using the Recover command
- Use Destroy to permanently remove items
- Use Purge to permanently remove all deleted items from the database

---

## Deploy

Deploys a project to a server or FTP location.

**Syntax:**
```
ss Deploy <project> <deployment location> [-H] [-I-] [-O-] [-R] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

---

## Destroy

Permanently removes a file or project.

**Syntax:**
```
ss Destroy <items> [-H] [-I-] [-Y] [-?]
```

**Project Rights:** You must have the Destroy project right to use this command.

**Notes:**
- This operation is permanent and cannot be undone
- The item must be deleted first before it can be destroyed
- Consider using Purge for batch destruction of deleted items

---

## Diff (File)

Shows the line-by-line differences between a Visual SourceSafe database master copy of a file and the corresponding file in your working folder.

**Syntax:**
```
ss Diff <file> [-B] [-C] [-D] [-DS] [-DT] [-H] [-I-] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Useful for reviewing changes before checking in
- Different output formats available with -D options

---

## Diff (Project)

Shows the differences between a Visual SourceSafe database master copy of a project and the corresponding project in your working folder.

**Syntax:**
```
ss Diff <project> [-DS] [-H] [-I-] [-N] [-O-] [-R] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Shows which files are different, added, deleted, or missing
- Use -R for recursive comparison of subprojects

---

## Dir

Shows a list of all files and subprojects in the specified project, or in the current project.

**Syntax:**
```
ss Dir <project> [-F-] [-H] [-I-] [-N] [-O-] [-R] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Similar to a directory listing in a file system
- Use -R to list recursively

---

## Filetype

Sets the file type for one or more files.

**Syntax:**
```
ss Filetype <files> <type> [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Visual SourceSafe stores binary and text files differently
- Common types: binary, text, unicode

---

## FindinFiles

Shows all occurrences of a specified string in one or more files, or in an entire project.

**Syntax:**
```
ss FindinFiles <string> <items> [-H] [-I-] [-N] [-O-] [-R] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Searches file contents, not file names
- Use -R to search recursively through subprojects

---

## Get

Retrieves read-only copies of the specified Visual SourceSafe files or projects to your working folder.

**Syntax:**
```
ss Get <items> [-G-] [-GF] [-GL] [-GS] [-GT] [-GTM] [-GTU] [-H] [-I-] [-N] [-O-] [-R] [-V] [-W] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Does not check out files (they remain read-only)
- Use Checkout if you need to modify files
- -GF forces get even if file is writable
- -R gets entire project tree recursively

**Examples:**
```
# Get latest version of TEST.C
ss Get TEST.C

# Get entire project recursively
ss Get $/ -R

# Get specific version
ss Get TEST.C -V5
```

---

## Help

Displays general help for using Visual SourceSafe.

**Syntax:**
```
ss Help
```

---

## History

Shows the history of a file or project in Visual SourceSafe.

**Syntax:**
```
ss History <items> [-B] [-F-] [-H] [-I-] [-L] [-N] [-O-] [-R] [-U] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Shows all changes, check-ins, labels, and other operations
- Use -V to limit history to specific version ranges
- Use -U to filter by user

**Examples:**
```
# Show full history
ss History TEST.C

# Show history from version 14 back to version 1
ss History TEST.C -V14

# Show history for date range
ss History $/test -Vd3/03/95;3:00p~3/03/95;9:00a
```

---

## Label

Assigns a label to the specified items (files or projects).

**Syntax:**
```
ss Label <items> [-C] [-H] [-I-] [-L] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Labels mark specific versions for easy reference
- Common use: marking release versions
- Use -V to label a specific version

**Example:**
```
# Label current version as Release 1.0
ss Label $/MyProject -LRelease_1.0
```

---

## Links

Shows the projects that share the specified file(s).

**Syntax:**
```
ss Links <file> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Displays all projects containing shared copies of the file
- Useful for understanding file dependencies

---

## Locate

Searches Visual SourceSafe projects for the specified files or projects.

**Syntax:**
```
ss Locate <items> [-F-] [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Searches the entire database
- Supports wildcards

---

## Merge

Combines the contents of two file copies that have the same name but are part of different projects.

**Syntax:**
```
ss Merge <source file> <destination file> [-C] [-G-] [-GF] [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Useful after branching when you want to incorporate changes from one branch to another
- Visual SourceSafe will attempt automatic merge, prompting for conflicts

---

## Move

Relocates a subproject from one parent project to another.

**Syntax:**
```
ss Move <source project> <destination project> [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right in both projects.

**Notes:**
- The entire subproject is moved, including all files and history
- The source project is removed from its original location

---

## Password

Sets the password for the current user.

**Syntax:**
```
ss Password <old password> <new password> [-Y] [-?]
```

**Notes:**
- Changes your Visual SourceSafe password
- Both old and new passwords are required

---

## Paths

Shows all share links for files that have been branched.

**Syntax:**
```
ss Paths <file> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Displays the history of sharing and branching for a file
- Useful for understanding file relationships

---

## Physical

Determines the physical location of a Visual SourceSafe-encrypted file in a Visual SourceSafe database.

**Syntax:**
```
ss Physical <items> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Shows the actual file path in the VSS database directory structure
- Primarily used for troubleshooting

---

## Pin

Marks files at a specific version number in the current project.

**Syntax:**
```
ss Pin <items> [-C] [-H] [-I-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Pinned files will not be updated by Get operations
- Useful for maintaining specific versions in a project
- Use Unpin to release pinned files

---

## Project

Displays the current project path.

**Syntax:**
```
ss Project [-Y] [-?]
```

**Notes:**
- Shows which project commands will operate on by default
- Use CP to change the current project

---

## Properties

Shows the properties of a Visual SourceSafe file or project.

**Syntax:**
```
ss Properties <items> [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Displays creation date, file type, size, and other metadata

---

## Purge

Permanently removes previously deleted files and projects from a Visual SourceSafe database.

**Syntax:**
```
ss Purge <items> [-H] [-I-] [-Y] [-?]
```

**Project Rights:** You must have the Destroy project right to use this command.

**Notes:**
- This operation is permanent and cannot be undone
- Items must be deleted before they can be purged
- Frees up database space

---

## Recover

Recovers files and projects that have been deleted.

**Syntax:**
```
ss Recover <items> [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- Restores deleted items that have not been destroyed or purged
- Items are restored to their original location

---

## Rename

Changes the name of a file or project.

**Syntax:**
```
ss Rename <old name> <new name> [-C] [-H] [-I-] [-N] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- Renames the item in the Visual SourceSafe database
- History is preserved with the renamed item

---

## Rollback

Undoes all changes since an earlier version of a file.

**Syntax:**
```
ss Rollback <file> [-G-] [-GF] [-H] [-I-] [-N] [-O-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Creates a new version that matches the specified earlier version
- Original versions are preserved in history
- Use -V to specify which version to roll back to

---

## Share

Makes the specified file or project part of the current project.

**Syntax:**
```
ss Share <source> [-C] [-E] [-H] [-I-] [-N] [-O-] [-R] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Add/Rename/Delete project right to use this command.

**Notes:**
- Creates a link between projects so they share the same file
- Changes to shared files affect all projects sharing them
- Use Branch to break a share link

---

## Status

Shows checkout information on files.

**Syntax:**
```
ss Status <items> [-F-] [-I-] [-N] [-O-] [-R] [-U] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Shows which files are checked out and by whom
- Use -U to filter by specific user

---

## Undocheckout

Cancels a Check Out operation, voiding all changes.

**Syntax:**
```
ss Undocheckout <items> [-G-] [-H] [-I-] [-N] [-O-] [-R] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

**Notes:**
- Discards all changes made to the working file
- The file is unlocked in the database
- Working file is restored to read-only

---

## Unpin

Undoes a Pin operation, ignoring files that are not pinned.

**Syntax:**
```
ss Unpin <items> [-C] [-H] [-I-] [-V] [-Y] [-?]
```

**Project Rights:** You must have the Check In/Out project right to use this command.

---

## View

Displays the contents of the specified file.

**Syntax:**
```
ss View <file> [-V] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Displays file contents to console
- Does not create a working file
- Use -V to view a specific version

---

## Whoami

Shows your current Visual SourceSafe user name.

**Syntax:**
```
ss Whoami [-Y] [-?]
```

---

## WorkFold

Sets the working folder.

**Syntax:**
```
ss WorkFold <project> <local folder> [-I-] [-O-] [-Y] [-?]
```

**Project Rights:** You must have the Read project right to use this command.

**Notes:**
- Establishes where Get and Checkout operations place files
- Each project can have its own working folder

---

## Command Options

The following options can be used with various Visual SourceSafe commands.

## -B Option

Overrides the Backup_Copies initialization variable for specific operations.

| Usage | Description |
|-------|-------------|
| -B | Creates a backup file |
| -B- | Does not create a backup file |

---

## -C Option

Allows you to enter a comment for operations.

| Usage | Description |
|-------|-------------|
| -C | Prompts for a comment |
| -C- | Suppresses comment prompt |
| -C"comment" | Specifies the comment text |

---

## -D Option

Specifies the output format for Diff operations.

| Usage | Description |
|-------|-------------|
| -D | Standard unified diff format |
| -DS | Standard summary format |
| -DT | Standard columnar output format |
| -DU | Unix-style diff format |

---

## -E Option

Specifies the Exclusive/Multiple checkout mode for files.

| Usage | Description |
|-------|-------------|
| -E | Enables exclusive checkout mode |
| -E- | Enables multiple checkout mode |

---

## -F Option

Controls whether folder names are displayed in output.

| Usage | Description |
|-------|-------------|
| -F | Includes folder names |
| -F- | Excludes folder names |

---

## -G Option

Controls whether Get operations retrieve files to the working folder.

| Usage | Description |
|-------|-------------|
| -G | Gets file to working folder (default location) |
| -G- | Does not get file |
| -GF | Overwrites writable working files |
| -GL | Gets file to specified folder |
| -GS | Gets file to shared network folder |
| -GT | Sets file's modification time to current time |
| -GTM | Sets file's modification time to modification time |
| -GTU | Sets file's modification time to check-in time |

---

## -H Option

Specifies request help for a command.

| Usage | Description |
|-------|-------------|
| -H | Displays help information |

---

## -I Option

Controls whether interactive mode is enabled.

| Usage | Description |
|-------|-------------|
| -I | Enables interactive mode (prompts for input) |
| -I- | Disables interactive mode (uses defaults) |

**Notes:**
- Use -I- in scripts and batch files to prevent prompting
- Required for automated operations

---

## -K Option

Keeps files checked out after checking them in.

| Usage | Description |
|-------|-------------|
| -K | Keeps file checked out after check-in |

---

## -L Option

Specifies a label for Label command.

| Usage | Description |
|-------|-------------|
| -L | Prompts for label |
| -L"label" | Specifies the label text |

---

## -N Option

Prevents output of file names during operations.

| Usage | Description |
|-------|-------------|
| -N | Suppresses file name output |

---

## -O Option

Redirects output to a file.

| Usage | Description |
|-------|-------------|
| -O | Prompts for output file name |
| -O- | Disables output redirection |
| -Ofilename | Redirects output to specified file |

---

## -P Option

Specifies a project to use with a command.

| Usage | Description |
|-------|-------------|
| -P | Specifies the current project |
| -Pproject | Specifies the named project |

---

## -Q Option

Suppresses output for a command line command.

| Usage | Description |
|-------|-------------|
| -Q | Specifies a quiet operation |

---

## -R Option

Makes commands that operate on projects recursive to subprojects.

| Usage | Description |
|-------|-------------|
| -R | Specifies a recursive operation |

**Notes:**
- A command will ignore this option if a file name or mask is specified

---

## -S Option

Overrides the Smart_Mode initialization variable for a particular command.

| Usage | Description |
|-------|-------------|
| -S | Enables Smart mode |
| -S- | Disables Smart mode |

---

## -U Option

Displays user information about a file or project.

| Usage | Description |
|-------|-------------|
| -U | Specifies the current user of the file or project |
| -Uusername | Specifies the named user |

---

## -V Option

Indicates the version of a file or project.

| Usage | Description |
|-------|-------------|
| -Vnumber | Specifies a version with the indicated version number |
| -Vddate | Specifies a version having the indicated date/time stamp |
| -Vllabel | Specifies a version having the indicated label |

**Notes:**
- You can specify a version number for one particular item by following that item with a semicolon and then the version number
- You can specify a version range, which must begin with the last version required

**Examples:**
```
# Displays versions starting with version 14 and going back to version 1
ss History -V14

# Displays versions 5, 4, and 3
ss History -V5~3

# Displays the version dated 2-29-92
ss History -Vd2-29-92

# Displays the version having the label Final Beta
ss History -VlFinal Beta

# Displays versions from 9 A.M. to 3 P.M. on 3/3/95
ss History $/test -Vd3/03/95;3:00p~3/03/95;9:00a

# Displays version 6 of Help.c
ss History -VHelp.c;6

# Displays Show.prg as it appeared on 2-29-92
ss History -VShow.prg;d2-29-92
```

---

## -W Option

Indicates if working copies are to be read/write or read-only.

| Usage | Description |
|-------|-------------|
| -W | Sets working copies to read/write |
| -W- | Sets working copies to read-only |

**Notes:**
- By default, when a file is not checked out, Visual SourceSafe makes your working copy read-only
- The Use_ReadOnly initialization variable in Ss.ini changes this default behavior

---

## -Y Option

Specifies a user name or user name and password.

| Usage | Description |
|-------|-------------|
| -Yusername | Specifies a user name |
| -Yusername,password | Specifies a user name and password |

**Notes:**
- Use this option if you want to execute a command as another user