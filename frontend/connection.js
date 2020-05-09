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
        newdiv.innerHTML = "<label><input type='checkbox' class='checkbox' name='sfdcObjects[]' value='" + data[i].Name + "' >" + data[i].Label + "<label>";
        form.appendChild(newdiv);
    }
}


function search(query) {

    query = query.toLowerCase();

    document.querySelectorAll(".checkbox").forEach(function (input) {

        if (query === "--show-all") {
            input.parentElement.classList.remove("hide");
            input.parentElement.classList.remove("show");
            input.parentElement.classList.add("show");
        } else if (input.value.toLowerCase().includes(query)) {

            input.parentElement.classList.remove("hide");
            input.parentElement.classList.remove("show");
            input.parentElement.classList.add("show");

        } else {
            input.parentElement.classList.remove("hide");
            input.parentElement.classList.remove("show");
            input.parentElement.classList.add("hide");
        }
    });
}

function spinnerOn() {
    document.getElementById("spinner").classList.remove("hide");
    document.getElementById("spinner").classList.add("spinner");
}

function spinnerOff() {
    document.getElementById("spinner").classList.remove("spinner");
    document.getElementById("spinner").classList.add("hide");
}