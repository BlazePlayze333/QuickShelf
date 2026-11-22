QUICK SHELF - SOURCE PACKAGE

Windows C# / .NET 10 / Windows Forms. Generated source, not compiled or tested here.

FILES
Program.cs, QuickShelf.csproj, app.ico: repository root.
.github/workflows/build.yml: exact workflow location.
.gitignore: keeps local build output out of Git.

GITHUB
Create a separate repository (for example QuickShelf). Extract this ZIP first.
Upload the root files. Create .github/workflows/build.yml with the included workflow contents.
Do not put build.yml at repository root. Verify the hidden .github directory is included if uploading via Git.
Commit to main. Actions > Build Quick Shelf > newest successful run > Artifacts > QuickShelf-win-x64.
Extract the artifact ZIP and run QuickShelf.exe.
Manual build: Actions > Build Quick Shelf > Run workflow (main).
Release: Releases > Draft a new release > new unused tag targeting main > Publish release.
The release event builds the tagged source and attaches QuickShelf.exe after the run succeeds.
Do not create the release on an older tag if you want newer code.

OPTIONAL LOCAL BUILD
Install .NET 10 SDK, then run from the extracted source folder:
dotnet publish QuickShelf.csproj -c Release -r win-x64 --self-contained true -o ReadyToRun
Open ReadyToRun/QuickShelf.exe.

USE
Opens the shelf on startup. Click the tray icon to show/hide it.
Click outside to hide; Escape or window X hides it too.
Right-click tray > Exit Quick Shelf to quit.
Click + Add: choose file/folder or paste an http(s) address, enter name, Save.
Drag files/folders onto the OPEN shelf: one item opens an editor; multiple items prompt once.
A plain text http(s) URL can also be dropped. Browser-specific drag formats may need copy/paste instead.
Right-click an item: Open, Edit/rename, Move up/down, Remove.
Removing only removes the shelf entry, not the original file.
One click opens an item. Select an item with right-click to edit without launching it.
Search filters names and targets.
Pin the executable in a stable location and don't run it as administrator for normal use.
The app does not register startup, global hotkeys, or file associations.
It does not auto-update. It does not contact GitHub while running.

SAVED DATA
%LOCALAPPDATA%/QuickShelf/shortcuts.json
This is the one persistent JSON file, separate from the EXE folder.
A temporary file is used while committing changes and then replaced/removed.
Don't delete the saved JSON unless you want to reset your shelf.
If it cannot be read, changes are disabled to prevent overwriting it.
Targets are stored as plain text; don't store passwords or secret-bearing URLs.
Only add items you trust: opening executables/scripts/shortcuts runs their associated actions.
Moving or deleting an original file can break its entry; use Edit to update the target.
An embedded .NET runtime can extract native components to the Windows temp .net directory.
Single EXE means one distributed executable, not zero disk writes.
