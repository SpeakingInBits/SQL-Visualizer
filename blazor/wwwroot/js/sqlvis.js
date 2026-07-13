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
    },
    /** Scroll rows into view (instant) then return their Y centres relative to the SVG element. */
    scrollAndMeasureConnector: function (leftRowId, rightRowId, svgId) {
        const leftRow = document.getElementById(leftRowId);
        const rightRow = document.getElementById(rightRowId);
        const svg = document.getElementById(svgId);
        if (!leftRow || !rightRow || !svg) return null;
        leftRow.scrollIntoView({ behavior: 'instant', block: 'nearest' });
        rightRow.scrollIntoView({ behavior: 'instant', block: 'nearest' });
        const svgRect = svg.getBoundingClientRect();
        const lRect   = leftRow.getBoundingClientRect();
        const rRect   = rightRow.getBoundingClientRect();
        return {
            leftY:  lRect.top  + lRect.height  / 2 - svgRect.top,
            rightY: rRect.top  + rRect.height  / 2 - svgRect.top
        };
    },
    /** Scroll an element into view smoothly, keeping it within its scroll parent. */
    scrollIntoView: function (id) {
        const el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    },

    /** Return an element's viewport rectangle {left, top, width, height}. */
    getRect: function (element) {
        if (!element) return null;
        const r = element.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height };
    }
};
