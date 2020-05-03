//// JavaScript source code

const { ConnectionBuilder } = require('electron-cgi');
const fs = require('fs')

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

function parseObjectsList(sfdcObjects) {
    data = JSON.parse(sfdcObjects);
    form = document.getElementById("sfdc-objects");

    for (let i = 0; i < data.length; i++) {

        var newdiv = document.createElement('div');
        newdiv.innerHTML = "<label><input type='checkbox' name='sfdcObjects[]' value='" + data[i].Name + "' >" + data[i].Label + "<label>";
        form.appendChild(newdiv);
    }
}

