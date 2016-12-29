# json-db

Used to create Databases out of JSON files.

In order to use this project in your solution, you will need to use the nuget packages available at:

1-  Azureoth.Json-db.Datastructures 1.0.0
    Command: "Install-Package SQLDatastructures"

2-  Azureoth.Json-db 1.0.0
    Command: "Install-Package SQLdb"


You will also need to add the following build options to your main project, and add the "third_party" folder into the root of your solution.

"buildOptions": {
    "copyToOutput": { "include": [ "../third_party/" ] }
}