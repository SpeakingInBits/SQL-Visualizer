// sqlvis.js — JS interop helpers for SqlVisualizer Blazor WASM

window.SqlVis = {
    /** Read a File object selected by an <input type="file"> as a Uint8Array. */
    readFileAsBytes: async function (inputElement) {
        const file = inputElement.files[0];
        if (!file) return null;
        const buf = await file.arrayBuffer();
        return new Uint8Array(buf);
    },

    /** Return the file name from a file input element. */
    getFileName: function (inputElement) {
        const file = inputElement.files[0];
        return file ? file.name : null;
    },

    /** Trigger a hidden file-input click. */
    triggerClick: function (element) {
        element.click();
    },

    /** Focus a DOM element. */
    focusElement: function (element) {
        if (element) element.focus();
    }
};
