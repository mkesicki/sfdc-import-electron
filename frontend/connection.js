//// JavaScript source code

const { ConnectionBuilder } = require('electron-cgi');

var connection = new ConnectionBuilder()
    .connectTo("dotnet", "run", "--project", "backend/SFDCImportElectron")
    .build();

connection.onDisconnect = () => {

    console.log("lost");
};

function log(message) {

    logArea = document.getElementById("log-messages");
    logArea.value = logArea.value + "\n" + message;
}
//connection.send("greeting", "Mom", (err, response) => {
//    console.log(response);
//    //connection.close();
//});
