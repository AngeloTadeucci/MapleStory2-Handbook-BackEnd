# MapleStory 2 Handbook - BackEnd

MapleStory 2 Handbook is a searchable database of items and NPCs in the popular MMORPG MapleStory 2. 

This project is the backend where it parses the data from the game, creates the database and populates your database. 

Also has a project, `NifToGltf`, that converts NIF models to glTF. Its native converter is the default. The older [Noesis](https://richwhitehouse.com/index.php?content=inc_projects.php&showproject=91) workflow is still available with `--noesis` for models the native converter cannot convert yet. See [NifToGltf/Native/README.md](NifToGltf/Native/README.md#default-mode-and-production-state).

## Required Technology

- .NET 6
- MySQL 8

## Installation

1. Get the Xml.m2d and Xml.m2h from your MapleStory2 Client and copy it to Maple2Storage/Resources. More detailed information can be found on [mapleme.me/docs](https://mapleme.me/docs/setup/resources-setup).
2. Configure your MySQL connection on **GameParser/QueryManager.cs**.
3. Run the GameParser project, it should populate your database.
