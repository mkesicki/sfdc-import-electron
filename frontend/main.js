const { app, BrowserWindow, systemPreferences, Menu, nativeTheme } = require('electron')
const path = require("path");
const querystring = require('querystring');

let mainWindow, loginWindow



global.data = null;


function createWindow() {
    
    //Create the browser window.
    mainWindow = new BrowserWindow({
        minWidth: 1024,
        minHeight: 768,
        webPreferences: {
            nodeIntegration: true,
            preload: path.join(__dirname, "preload.js") // use a preload script
        },
    });

    loginWindow = new BrowserWindow({
        minWidth: 800,
        minHeight: 600,
        webPreferences: {
            nodeIntegration: true,
            preload: path.join(__dirname, "preload.js") // use a preload script
        },
        parent: mainWindow,
        modal: true,
        show: true,
        autoHideMenuBar: true
    });

    // and load the index.html of the app.
    let data = { "darkMode": nativeTheme.shouldUseDarkColors }
    mainWindow.loadFile('frontend/index.html', { query: { "data": JSON.stringify(data) } })
    loginWindow.loadFile('frontend/indexLogin.html', { query: { "data": JSON.stringify(data) } })

    const mainMenu = Menu.buildFromTemplate(menuTemplate);
    Menu.setApplicationMenu(mainMenu);

    // Emitted when the window is closed.
    mainWindow.on('closed', function () {      
        mainWindow = null
        loginWindow = null
    })

    loginWindow.webContents.on('will-navigate', function (event, newUrl) {

        if (newUrl.includes("localhost/callback") && !newUrl.includes("client_id")) {

            const data = querystring.parse(newUrl)

            var pattern = /access_token=(.+)&instance/;
            var result = decodeURIComponent(newUrl).match(pattern)

            token = result[1]
            data.token = token
            console.log(data)
            global.data = data;
            loginWindow.close()
            loginWindow = null          
        }        
    });
}

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', createWindow)

// Quit when all windows are closed.
app.on('window-all-closed', function () {

    // On macOS it is common for applications and their menu bar
    // to stay active until the user quits explicitly with Cmd + Q
    if (process.platform !== 'darwin') {
        app.quit()
    }
})

app.on('activate', function () {
    // On macOS it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if (mainWindow === null) {
        createWindow()
    }
})

function showAboutWindow() {
    addWindow = new BrowserWindow({
        width: 300,
        height: 200,
        title: 'About',
        autoHideMenuBar: true
    });
    addWindow.loadURL(`file://${__dirname}/about.html`);
    addWindow.on('closed', () => addWindow = null);
}

const menuTemplate = [
    {
        label: 'File',
        submenu: [
            { role: 'quit' },    
        ]
    },
    {
        label: 'Help',
        submenu: [

            {
                label: 'About',
                accelerator: process.platform === 'darwin' ? 'Command+F1' : 'Ctrl+F1',
                click() {
                    showAboutWindow();
                }
            }
        ]
    }
];

if (process.platform === 'darwin') {
    menuTemplate.unshift({});
}

if (process.env.NODE_ENV !== 'production') {
    menuTemplate.push({
        label: 'View',
        submenu: [
            { role: 'reload' },
            { role: 'toggleDevTools' },
        ]
    });
}