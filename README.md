# Electron Salesforce Import Application

This is just a fun project to check how to work with Electron.
The idea behind is to create GUI for https://github.com/mkesicki/sfdc-import
Do not use it on production :)
If you see any login data in code there are for trailhead ...
Connected application in salesforce need to be created (you need client_id, client_secret)

TODOs:
- Add Drag & Drop for file
- Proper/better way to load windows/files
- Yes, design I know :)
- Think about better handling salesforce limit for concurrent request
- Figure out wrong counting of success and errors rows
- Restart / take steps back in GUI
- As always error handling
- Async logs saving?
- Handle refreshing oauth token ?