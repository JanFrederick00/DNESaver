# DNESaver

This project is a savegame editor for the game [Lost Records: Bloom & Rage](https://store.steampowered.com/app/1902960/Lost_Records_Bloom__Rage/).

## How to use

- Download a release or build it yourself
- Acquire a copy of `oo2core_5_win64.dll` and place it in the directory with the `DNESaver.exe`.
- Launch the Tool.

The Tool will automatically detect your savegame directory (under `LocalAppData/Bloom&Rage`). 
It will create a backup directory and make sure the savegame currently in slot 0 is backed up.
(The backup will only be created if the savegame is compressed, which it should be on up-to-date versions of the game).

The save file will be decompressed (if necessary) and a working copy will be created in the save folder.

You can now click on the button to open the save editor.

### Editing game Facts
On the first tab the game's "facts" may be edited. 

Each fact is a piece of information about the choices made by the player.
To ensure that the player can re-play every chapter, a copy of the choices at that point is saved for each of the game's 44 chapters.

As a result of this structure, the editor shows you a history for each fact. Most are only set once, but some are modified multiple 
times over the course of a playthrough.

Note: the scene names displayed in the history are always one scene before the one that contains the new values, because the values refer to the 
state at the start of the scenes.

It is currently not possible to only change the value of a fact in a specific scene or from a specific scene onwards. 
If you select "Change Value", all instances of this fact - i.e. for every scene which had a value for this fact - will be changed.

### Editing relationship values
The second tab is used to edit the relationship between the characters.

The main property is the relationship level, which corresponds to those seen on the stats screen (though those values are stored among the facts).
each relationship defines a "growth" and a "decay" value, plus counters for both. It is currently unknown how exactly the game uses these.

Note that the values on the relationship panel (excluding the counters) are limited between 0 and 255.


Read on if you want the technical details on how the game saves progress

### Some interesting Facts to modify [SPOILERS!]:
<details>
  <summary>Spoiler warning</summary>

- `S4500_NoraLeaves` is set to `true` if Nora has decided to leave. Set in scene 2-7 "Nora's Grief" but doesn't come into effect until three or four scenes later.
- `S1000_DIA_CatColour`: change cat color (and name). 
- `Sxxxx_BAR_Ending_IsPresent_SUM`: set to `true` if Nora is still present at the unboxing. If you set the above value, you probably need to set this accordingly. During a regular playthrough this will be the same as `S4500_NoraLeaves`
- `Sxxxx_BAR_Ending_IsPresent_AUT`: set to `true` if Autumn has decided that she stays. This is set in scene `2-16 Enter the Void` and used in the next one (so you can set this and replay from that scene to get another ending). I suggest you try out some of the combinations of these last two, the differences are really interesting.
  
</details>

# Note
I got the code that uses the oodle dll from somewhere. That was on one of two days where I was trying to find out what compression method they used and failed. I therefore cannot remember where I got this code from, and at this point I am way to tired to re-trace my steps. So if the code in Oodle/imports.cs or Oodle/oodle.cs looks familiar to you (or if you wrote it), let me know so I can add the necessary attributions.

# Savegame structure

Savegames are stored (at least on Windows) in the LocalAppData folder. A subfolder `Bloom&Rage` is created which contains a single `Saved` subfolder.

Most of the subfolders of that folder are empty (barring any crashes), and all games are located in a subfolder called `SaveGames`. 

This folder contains a save folder for each steam user-id.

This is the main save directory relative to which we will be talking from now on.

### MachineSettings.sav

The MachineSettings file stores the graphics settings.
It is the only settings file which is not specific to a save slot.

### 0UserSettings.sav

This file contains the other settings. It is savegame specific, just like the main save file.

### 0GameSave.sav

This is the save file which contains all data about the save slot's progress and choices, except the camcorder videos and thumbnails.
These are located in the 0Videos subfolder.

### Save File Structure
All three save files are the same format. The only difference is that the main save file is compressed.

Each File starts with an 8 byte long header which in ASCII equals the string `@DNESAV@`. 

If the File is compressed the next eight bytes are the constant  `C3 B0 E9 18 B5 6E 0C 59`. It is currently unknown what this signifies.
The next four bytes represent the length of the decompressed file (minus the `@DNESAV@`.) (All Values are little-endian).
The subsequent four bytes are the length of the compressed file (always equals to the (file size) - (24 bytes)).
The rest of the file is compressed using the `OodleLZ` compression method (which requires knowledge of the total length of the decompressed data).

If the file is not compressed, the same data that would be stored in this compressed block follows the header directly.
As this block always seems to start with the constant 0x0000000000000004, this can be detected.

The game has no problem loading an uncompressed save file, which is why this tool only decompresses the file.

As the save files contain a lot of text, the compression can be quite efficient. My save game was less that 100KiB in size in compressed format, while 
the uncompressed file is more than 10 MiB long.

Now for the real save data:

- Strings are stored in a mix of Pascal and C styles. The length (4 bytes) is followed by the ASCII data and a null terminator (which is counted in the length byte).
- Occasional `0x00` bytes are sprinkled in liberally. In this documentation I'm leaving them out for simplicity's sake but have a look at my parsing code for details.

The save file starts with the (8 byte) constant 0x04 of unknown purpose. 
This is followed by a data type (string), which is `UNIGameSaveData` for the main save file.

Another unknown value follows (4 bytes, `0x03F1` on my tests), as well as the Engine Version (string). (`5.2.0-0+UE5`).

For the most part, values in the configuration file are stored like this:

|index|length|purpose|
|---|---|---|
|0|variable|Property Name (string)|
|1|variable|Data Type (string)|
|2|8|Data Length, though not accurate for all data types|

Followed by the data itself.

Some Data types are:
- `NameProperty`, `StrProperty`: one null byte followed by the value as string (see above)
- `IntProperty` `FloatProperty` `ByteProperty` `Int64Property` `UInt32Property`: one null byte followed by the value. The length parameter is accurate.
- `BoolProperty`: same as `ByteProperty` except the null byte and the value byte are swapped.
- `ArrayProperty`: The Data type of the array elements is followed by a null byte and the number of elements as a 4 byte integer. The data follows. If the content of the array is `IntProperty` each element will be 4 bytes long. If the Data type is `NameProperty` then each value will be a string, as detailed above. Most arrays are of type `StructProperty` for which the "struct header" (see below) is only present on the first element.
- `StructProperty`:  This data type is present in two ways; 
    - If it occurs normally (top level or as a member in another struct), it consists of the class name of the data followed by 17 empty bytes. The struct members are then listed (just as on the top level). The String "None" is used to terminate the struct.
    - Exception: If the struct is a built-in one (the data type does not start with `UNI`), then it is not made up of other properties but contains the raw data after the header. Use the specified length and pray that it is correct.
	    - These include: DateTime (in 100ns increments afer 01/01/01), Color, Quat, Vector (the last two store all components as `double` for some reason)
	- If the struct's DataLength is `0x00` (even though it is followed by the 17 bytes of nothing), then the `None` terminator is skipped. If one occurs now it is part of the struct one level higher.
	- In an Array only the first struct has the (class name + 17 byte) header. All subsequent array elements only store the fields. The caveat with the empty struct does not apply here of course.
	- StructProperties as Keys in an array are so complicated that I haven't figured them out properly. Most of the times that means that each map key will be exactly 16 bytes long with no header and represent a GUID (though not using the standard order, it's just four LE int32s).
	- But sometimes a struct property as a key of a map is a full struct with header etc. I just read the first of the four integers and if it is smaller than 256 i use this special case. There's only a handful of times where this is used though, and not for anything interesting.
- `MapProperty`: The Datatype of the key and of the values are given as strings. 5 empty bytes follow, as well as the number of elements (as uint32LE again). 
    - Key-types could be `NameProperty` or `StructProperty`. See above. If the key is a name it will just be a string.
	- Value-types could be any of `NameProperty` (where each value is just a string), `StructPropery` (where the header is always missing, not just on every subsequent element),`BoolProperty` (each element is a single byte), `ByteProperty`, `IntProperty` and `FloatProperty` (1/4 byte values, no padding).
	
Maybe this makes sense if looked at the right way. But I have spent the last four days doing nothing but parsing this format and I am tired.
My tool reads all the empty bytes and warns on the console if they are not empty.

Encoded in this way is a complex data structure. 
It would be too much effort for this to be a comprehensive description, so here are the highlights:

- Save data is stored for each of the 44 scenes in the Map `SceneSnapshots`. Each one contains `fact`-Data (multiple nested maps). Facts can be part of the `BoolFacts`, `IntFacts`, `EnumFacts` or `FloatFacts` maps (though almost none are `Float`).
- Character Relationships are stored here as well. Some of them seem to be unused (`Swann & Bartender | PRESENT` for example.) The game keeps track of the relationship between Dylan & Corey as well as Autumn & Nora. 
- The Element `CurrentSnapshot` on the root level conforms to the same structure as the scene snapshots. Some facts are only stored here. 


### How are we modifying the save file?

Because of the complex structure I did not want to implement & debug a serializer for it. 
This implementation just stores the offset of any changeable value and then modifies it directly in the (decompressed) save file. 
(There is a sanity check to see if the old value matches what was expected).
This means that we cannot add facts or change the structure of the file (but that isn't necessary anyway).
	
	
