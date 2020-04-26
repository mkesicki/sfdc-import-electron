//// JavaScript source code

const { ConnectionBuilder } = require('electron-cgi');

//let _connection = null;

//function setupConnectionToRestartOnConnectionLost() {
//    _connection = new ConnectionBuilder().connectTo('dotnet', 'run', '--project', 'SFDCImport').build();
//    _connection.onDisconnect = () => {
//        alert('Connection lost, restarting...');
//        setupConnectionToRestartOnConnectionLost();
//    };
//}

//setupConnectionToRestartOnConnectionLost();

let connection = new ConnectionBuilder()
    .connectTo("dotnet", "run", "--project", "backend/SFDCImportElectron")
    .build();

connection.onDisconnect = () => {

    console.log("lost");
};

connection.send("greeting", "Mom", (err, response) => {
    console.log(response);
    //connection.close();
});
