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
    },

    /** Measure the first element matching a CSS selector for the onboarding tour.
     *  Returns {left, top, width, height, vw, vh} or null if not found. */
    measureTarget: function (selector) {
        const vw = window.innerWidth, vh = window.innerHeight;
        if (!selector) return { left: 0, top: 0, width: 0, height: 0, vw, vh };
        const el = document.querySelector(selector);
        if (!el) return null;
        const r = el.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height, vw, vh };
    },

    /** Notify a .NET object (via `OnViewportChanged`) whenever the window resizes. */
    watchResize: function (dotnetRef) {
        this.unwatchResize();
        let timer = null;
        this._resizeHandler = () => {
            clearTimeout(timer);
            timer = setTimeout(() => dotnetRef.invokeMethodAsync('OnViewportChanged'), 80);
        };
        window.addEventListener('resize', this._resizeHandler);
    },

    unwatchResize: function () {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            this._resizeHandler = null;
        }
    }
};
