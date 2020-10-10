window.electron = require('electron');

window.set = function (name, value) {

    window.localStorage.setItem(name, value);
}