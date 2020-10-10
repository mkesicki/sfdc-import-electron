const electron = window.electron;
const { ConnectionBuilder } = require('electron-cgi');
const fs = require('fs');
const UI = require('lockui');
const { remote } = electron
const dialog = remote.dialog;
const WIN = remote.getCurrentWindow();

var connection = new ConnectionBuilder()
    .connectTo("dotnet", "run", "--project", "backend/SFDCImportElectron")
    .build();

connection.onDisconnect = () => {

    console.log("lost");
};

function log(message) {
    logArea = document.getElementById("log-messages");
    logArea.value = logArea.value + "\n" + message;
    logArea.scrollTop = 99999;
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
    query = query.trim();
    query = query.replace(/ /g, "");

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
    UI.lock({ text: 'Processing...' })
}

function spinnerOff() {
    document.getElementById("spinner").classList.remove("spinner");
    document.getElementById("spinner").classList.add("hide");
    UI.unlock();
}

function logError(title, error) {

    log(title);
    log(error.Message);
    console.log(log);
    spinnerOff();
    throw new Error();
}

function checkStatus(immediate) {

    let wait = (immediate) ? 1 : 1000;
    
    setTimeout(() => {        
        connection.send("getStatus", (err, response) => {

            if (err) logError("Caramba, something is not ok", err);

            data = JSON.parse(response);
            console.log(response);

            log(`Processing ${data.processed} of ${data.all} rows`);

            if (data.isReady === "True") {
                //spinnerOff();
                log("Saving logs (this make take a while, please be patient)...")
                //save logs
                connection.send("saveLogs", (err, response) => {

                    if (err) logError("Caramba, something is wrong during logs saving", err);
                    log("Logs saved");

                    spinnerOff();
                });

                return;
            } else {
                checkStatus();
            }
        });
    }, wait);
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

    var span2 = document.createElement("span");
    span2.textContent = " (Delete) ";
    span2.classList.add('removeMapping');
    span2.addEventListener("click", (event) => {

        const id = event.target.parentNode.children[0].getAttribute("id");
        event.target.parentNode.innerHTML = "";

        sourceElement = document.getElementById(id);
        sourceElement.parentNode.classList.remove("hide");
    });
    
    ev.target.appendChild(span);
    ev.target.appendChild(span2)
    
    element.parentNode.classList.add('hide');
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
            row.innerHTML = "<td class='cellSource'>" + header[i] + "</td><td class='cellTarget " + header[i] + "'  ondrop='dropHandler(event)' ondragover='dragoverHandler(event)' >&nbsp;</td>"; //dropZone
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
                td.innerHTML = "<td><span id='" + metadata[i].Key + "_" + values[j] + "' data-parent='" + metadata[i].Key + "' class='draggableColumn " + metadata[i].Key + "_" + values[j] + "' draggable='true' ondragstart='dragstartHandler(event)' >" + values[j] + "</span></td>";
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

        document.getElementById("sfdc-objects-search-box").addEventListener("keyup", function () {
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


function initialize() {

    let form = document.getElementById("main-form");
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

    log("Initialize salesforce...")

    spinnerOn();    

    let file_to_parse = document.getElementById("file_to_parse").files[0].name;
    let data = [];

    settings = remote.getGlobal('data')

    data[0] = settings.token
    data[1] = settings.instance_url
    data[2] = file_to_parse;

    connection.send("initialize", JSON.stringify(data), (err, response) => {

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

function createMapping() {

    var data = {};
    var children = [];

    rows = document.querySelectorAll("#file-objects-table tr");
    parent = document.querySelector(".checkboxParent:checked");
    columnsCount = document.querySelectorAll(".checkboxParent").length;

    if (parent == null) {
        alert("Please select parent object before start processing.");

        return;
    }

    for (var i = 0; i < rows.length; i++) {

        if (i === 0) continue;

        var map = {};

        to = rows[i].cells[1];
        
        
        column = to.textContent.split('.');
        console.log(column);

        map.from = rows[i].cells[0].textContent;
        map.toObject = column[0];
        map.toColumn = column[1];
        if (map.toColumn) {
            map.toColumn = map.toColumn.replace(" (Delete) ", "");
        }

        children.push(map);
    }
    data.parent = (parent) ? parent.value : null;
    data.size = columnsCount;
    data.mapping = children;

    console.info(data);
    console.info(JSON.stringify(data));

    return data;
}

function parseFile(event) {

    data = createMapping();
    log("Parse file starting...");
    spinnerOn();
    connection.send("parse", JSON.stringify(data), (err, response) => {
        
        if (err) logError("Something very bad happen!", err);
        console.log(response);
        
        checkStatus(true);
    });
}

function saveFile(event) {

    const options = {

        title: "Save mapping file",
        defaultPath: `${__dirname}/../mapping.json`,
        buttonLabel: "Save Mapping File",
        filters: [
            { name: 'Mapping json', extensions: ['json'] },
            { name: 'All Files', extensions: ['*'] }
        ]
    }

    dialog.showSaveDialog(WIN, options)
        .then(file => {            
            if (!file.canceled) {
                data = createMapping();
                fs.writeFile(file.filePath.toString(),
                    JSON.stringify(data), function (err) {
                        if (err) throw err;
                        console.log(file);
                        log("Mapping saved in: " + file.filePath.toString());
                    });
            } 
        }).catch(err => {
            log("File saving error!");
            console.log(err)
        })
}


function loadFile(event) {

    const options = {
        title: "Open mapping file",
        //Placeholder 2
        defaultPath: `${__dirname}/../mapping.json`,
        //Placeholder 4
        buttonLabel: "Open Mapping File",
        //Placeholder 3
        filters: [
            { name: 'Mapping json', extensions: ['json'] },
            { name: 'All Files', extensions: ['*'] }
        ]
    }

    dialog.showOpenDialog(WIN, options)
        .then(filePaths => {

            console.log(filePaths);
            fs.readFile(filePaths.filePaths[0], 'utf-8', (err, data) => {

                if (err) {
                    log("An error ocurred reading the file :" + err.message);
                    console.log(err);
                    return;
                }

                // handle the file content 
                parseFromFile(JSON.parse(data))
            })
        }).catch(err => {
            log("File opening error!");
            console.log(err)
        })    
}

function parseFromFile(fileContent) {
    //parse the file
    log("Parsing loaded mapping");
    console.log(fileContent);
    checkboxes = document.querySelectorAll(".checkboxParent").forEach(function (element) {
        element.checked = (element.value == fileContent.parent) ? true : false;
    });

    fileContent.mapping.forEach(function (element) {
        console.log(element);
        sourceElement = document.querySelector("." + element.toObject + "_" + element.toColumn);
        console.log(sourceElement);
        sourceElement.parentNode.classList.add("hide");

        targetElement = document.querySelector('.' + element.from);

        html = "<td><span id='" + element.toObject + "_" + element.toColumn + "'data-parent='" + element.toObject + "' class='draggableColumn " + element.toObject + "_" + element.toColumn + "' draggable='true' ondragstart='dragstartHandler(event)' >" + element.toObject + "." + element.toColumn + "</span><span class='removeMapping'> (Delete) </span></td>";

        targetElement.innerHTML = html;
    });
}