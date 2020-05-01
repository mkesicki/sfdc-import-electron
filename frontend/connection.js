//// JavaScript source code

const { ConnectionBuilder } = require('electron-cgi');

var connection = new ConnectionBuilder()
    .connectTo("dotnet", "run", "--project", "backend/SFDCImportElectron")
    .build();

connection.onDisconnect = () => {

    console.log("lost");
};

function abc() {
    alert('dasdas@!');
}

//connection.send("greeting", "Mom", (err, response) => {
//    console.log(response);
//    //connection.close();
//});
