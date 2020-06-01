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

function logError(title, error) {

    log(title);
    log(error.Message);
    console.log(log);
    spinnerOff();
    throw new Error();
}

function ParentSelected() {
    checkboxes = document.querySelectorAll(".checkboxParent").forEach(function (element) {
        element.checked = false;
    });

    this.checked = true;
};

function dropHandler(ev) {
    ev.preventDefault();
    const id = ev.dataTransfer.getData("id");

    element = document.getElementById(id);
    var span = element.cloneNode(true);

    span.textContent = span.getAttribute('data-parent') + "." + span.textContent;

    ev.target.innerHTML = "";
    ev.target.appendChild(span);
    
    //element.classList.add('hide');
    element.parentElement.remove();
    element.remove();
}

function dragoverHandler(ev) {
    console.info('dragover');
    ev.preventDefault();
    ev.dataTransfer.dropEffect = "move"    
}

function dragstartHandler(ev) {
    console.info('we are moving')
    console.info(ev.target);
    ev.dataTransfer.setData("id", ev.target.id);
    ev.dataTransfer.dropEffect = "move";
}

function addMapping(header, metadata) {
    console.log(metadata);
    fs.readFile('frontend/mapping.html', (err, data) => {

        if (err) logError("Load mapping file error", err);
        document.getElementById('main-container').innerHTML = data;

        //display columns from CSV file
        for (var i = 0; i < header.length; i++) {
            container = document.getElementById("file-objects-table");

            var row = document.createElement('tr');
            row.innerHTML = "<td class='cellSource'>" + header[i] + "</td><td class='cellTarget'  ondrop='dropHandler(event)' ondragover='dragoverHandler(event)' >&nbsp;</td>"; //dropZone
            container.appendChild(row);
        }

        //display columns from metadata
        parentContainer = document.getElementById("sfdc-objects-container");       

        for (var i = 0; i < metadata.length; i++) {
            container = document.createElement('table');
            container.classList.add('sfdc-object-table');
            var thead = document.createElement('thead');
            var row = document.createElement('tr');
            var rowInput = document.createElement('tr');

            //set header row
            th = document.createElement("th");
            td = document.createElement("td");
            th.innerHTML = "<th>" + metadata[i].Key + "</th>";
            td.innerHTML = "<td class='cellMetadata'><label><input type='checkbox' value='" + metadata[i].Key + "' class='checkboxParent' />Parent</label></td>";
            row.appendChild(th);
            rowInput.appendChild(td)

            thead.appendChild(row);
            container.appendChild(thead);
            container.appendChild(rowInput);

            var values = metadata[i].Value.sort();
            
            for (var j = 0; j < values.length; j++) {
                var row = document.createElement('tr');
                td = document.createElement("td");
                td.innerHTML = "<td><span id='cell_" + i + "_" + j + "' data-parent='" + metadata[i].Key + "' class='draggableColumn' draggable='true' ondragstart='dragstartHandler(event)' >" + values[j] + "</span></td>";
                td.classList.add('cellMetadata');
                //td.setAttribute('data-parent', metadata[i].Key);
                
                row.appendChild(td);
                container.appendChild(row); 
            }

            parentContainer.appendChild(container);
        }
               

        document.querySelectorAll(".checkboxParent").forEach(function (element) {
            element.addEventListener("change", ParentSelected);
        });
    });

    spinnerOff();
} //add mapping





function loadList(sfdcObjects) {
    fs.readFile('frontend/list.html', (err, data) => {

        if (err) logError("Something very bad happen!", err);

        document.getElementById('main-container').innerHTML = data
        parseObjectsList(sfdcObjects);

        document.getElementById("sfdc-objects-search-box").addEventListener("change", function () {
            let value = this.value;

            if (value.length > 0 && value.length < 3) {


            } else if (value.length == 1) {
                //reset all
                search("--show-all");
            } else {
                //filter
                search(value);
            }
        });

        var fields = [];

        document.getElementById("createMapping").addEventListener("click", function () {

            log("Let's do the mapping");
            spinnerOn();

            connection.send("getHeaderRow", (err, header) => {

                log("Get header row");
                if (err) logError("Caramba, something is not ok", err);

                form = document.getElementById("sfdc-objects");

                for (var i = 0; i < form.elements.length; i++) {
                    if (form.elements[i].checked) {
                        fields.push(form.elements[i].value);
                    }
                }

                header = JSON.parse(header);
                console.log(header);

                spinnerOff();

                if (fields.length > 0) getMetadata(fields, header);
            }); //get header
        });

        function getMetadata(fields, header) {

            spinnerOn();
            log("Get medatada");
            var metadata;
            connection.send("getMetadata", JSON.stringify(fields), (err, mapping) => {

                if (err) logError("Good I am not Japanese ;)", err);

                metadata = JSON.parse(mapping);
                console.log(metadata);

                addMapping(header, metadata);
            }); // get metadata
        }
    }) // read list html
}


function login() {


    let form = document.getElementById("login-form");
    if (form.checkValidity() === false) {

        for (var i = 0; i < form.elements.length; i++) {
            input = form.elements[i];

            if (input.checkValidity() === false) {
                console.info(input.validationMessage);
                input.classList.add('error');
                //input.setCustomValidity(input.validationMessage);
            }
        }

        //form has error do not send it
        return;
    }

    log("Login to salesforce...")

    spinnerOn();

    let username = document.getElementById("username").value;
    let password = document.getElementById("password").value;
    let client_id = document.getElementById("client_id").value;
    let client_secret = document.getElementById("client_secret").value;
    let login_url = document.getElementById("login_url").value;
    let file_to_parse = document.getElementById("file_to_parse").files[0].name;
    let data = [];

    data[0] = username;
    data[1] = password;
    data[2] = client_id;
    data[3] = client_secret;
    data[4] = login_url;
    data[5] = file_to_parse;

    //let username = document.getElementById("username").value;
    connection.send("login", JSON.stringify(data), (err, response) => {

        if (err) logError("Something very bad happen!", err);

        console.log(response);
        log(response);

        spinnerOff();

        log("Get salesforce objects");

        connection.send("getSFDCObjects", (err, response) => {

            if (err) logError("Something very bad happen!", err);
            let sfdcObjects = response;
            //console.log(sfdcObjects);
            log("List of objects retrieved...");
            loadList(sfdcObjects);
        });
    });
}


function parseFile(event) {

    var data = {};
    var children = [];

    rows = document.querySelectorAll("#file-objects-table tr");
    parent = document.querySelector(".checkboxParent:checked");

    for (var i = 0; i < rows.length; i++) {

        if (i === 0) continue;

        var map = {};

        to = rows[i].cells[1];
        column = to.textContent.split('.');

        map.from = rows[i].cells[0].textContent;
        map.toObject = column[0];
        map.toColumn = column[1];

        children.push(map);
    }
    data.parent = (parent) ? parent.value : null;
    data.mapping = children;

    console.info(data);
    console.info(JSON.stringify(data));
}