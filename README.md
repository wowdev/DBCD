# DBCD
C# library for reading and writing [DBC](https://wowdev.wiki/DBC)/[DB2](https://wowdev.wiki/DB2) database files from World of Warcraft with built-in support for [WoWDBDefs](https://github.com/wowdev/WoWDBDefs) definitions.

## Features
- Reading of `WDBC` ([.dbc](https://wowdev.wiki/DBC)) and `WDB2`-`WDB6`, `WDC1`-`WDC5` ([.db2](https://wowdev.wiki/DB2)).
- Experimental writing (`WDC3` works, the others likely will too but are largely untested with actual WoW clients).
- Applying of hotfixes (DBCache.bin).

## Limitations
- _(Reading/Writing)_ Relies on [WoWDBDefs](https://github.com/wowdev/WoWDBDefs) (DBDs) for table structures, can not load tables without DBDs (yet).
- _(Writing)_ Does not support writing out DB2s with multiple sections.

## Example Usage
```csharp
// A FilesystemDBCProvider to load DBCs/DB2s from a directory on disk. 
var localDBCProvider = new FilesystemDBCProvider("D:/DBC");

// A FilesystemDBDProvider to load DBDs from a folder, you can also use GithubDBDProvider to download them directly from GitHub.
var localDBDProvider = new FilesystemDBDProvider("D:/WoWDBDefs/definitions");

// A new DBCD instance with the specified DBC/DBD provider.
var dbcd = new DBCD(localDBCProvider, localDBDProvider);

// Loads Map.db2 (note the table name without extension) for build 11.0.2.56044 (build might be needed to load correct definition).
var storage = dbcd.Load("Map", "11.0.2.56044");

// Get the row with ID 2552.
var row = storage[2552];

// Outputs "Khaz Algar (Surface)".
Console.WriteLine((string)row["MapName_lang"]);
```
 
