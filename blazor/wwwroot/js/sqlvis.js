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

    /** Route a pointer's events to an element until release (drag outside the element). */
    capturePointer: function (element, pointerId) {
        try { if (element) element.setPointerCapture(pointerId); } catch { /* pointer already gone */ }
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
     *  When `reveal` is set, a target that is off-screen is first scrolled into view.
     *  Returns {left, top, width, height, vw, vh} or null if not found. */
    measureTarget: function (selector, reveal) {
        const vw = window.innerWidth, vh = window.innerHeight;
        if (!selector) return { left: 0, top: 0, width: 0, height: 0, vw, vh };
        const el = document.querySelector(selector);
        if (!el) return null;
        // On small screens the layout stacks and the target may be scrolled out of
        // view; bring it to the top so the docked card below doesn't cover it.
        const r0 = el.getBoundingClientRect();
        if (reveal && (r0.top < 0 || r0.bottom > vh || r0.left < 0 || r0.right > vw))
            el.scrollIntoView({ behavior: 'instant', block: 'start', inline: 'nearest' });
        const r = el.getBoundingClientRect();
        return { left: r.left, top: r.top, width: r.width, height: r.height, vw, vh };
    },

    /** Notify a .NET object (via `OnViewportChanged`) whenever the window resizes
     *  or anything on the page scrolls (so a spotlight follows its target). */
    watchResize: function (dotnetRef) {
        this.unwatchResize();
        let timer = null;
        this._resizeHandler = () => {
            clearTimeout(timer);
            timer = setTimeout(() => dotnetRef.invokeMethodAsync('OnViewportChanged'), 80);
        };
        window.addEventListener('resize', this._resizeHandler);
        document.addEventListener('scroll', this._resizeHandler, true);
    },

    unwatchResize: function () {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            document.removeEventListener('scroll', this._resizeHandler, true);
            this._resizeHandler = null;
        }
    }
};
