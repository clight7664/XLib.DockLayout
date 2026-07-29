const xlibDockLayoutInstances = new Map();
const xlibDockLayoutDirection = Object.freeze({ Center: 0, Left: 1, Right: 2, Top: 3, Bottom: 4 });
const xlibDockLayoutRegion = Object.freeze({
    LeftDockUpper: 0,
    LeftDockLower: 1,
    RightDockUpper: 2,
    RightDockLower: 3,
    BottomLeftDock: 4,
    BottomRightDock: 5
});

export function initialize(instanceId, rootElement, dotNetRef, options) {
    destroy(instanceId);
    if (!instanceId || !rootElement) return false;
    const abortController = new AbortController();
    const state = {
        instanceId,
        rootElement,
        dotNetRef,
        options: options ?? {},
        abortController,
        resizeObserver: null,
        rafId: 0,
        drag: null,
        lastResize: { width: 0, height: 0 }
    };
    xlibDockLayoutInstances.set(instanceId, state);

    rootElement.addEventListener('pointerdown', event => xlibDockLayoutHandlePointerDown(state, event), { signal: abortController.signal });
    window.addEventListener('pointermove', event => xlibDockLayoutHandlePointerMove(state, event), { signal: abortController.signal, passive: false });
    window.addEventListener('pointerup', event => xlibDockLayoutHandlePointerUp(state, event), { signal: abortController.signal });
    window.addEventListener('pointercancel', () => xlibDockLayoutCancelDrag(state, 'pointer-cancel'), { signal: abortController.signal });
    window.addEventListener('keydown', event => {
        if (event.key === 'Escape' && state.drag) {
            event.preventDefault();
            xlibDockLayoutCancelDrag(state, 'escape');
        }
    }, { signal: abortController.signal });
    rootElement.addEventListener('wheel', event => xlibDockLayoutHandleWheel(event), { signal: abortController.signal, passive: false });

    observeResize(instanceId, rootElement);
    setVisibilityObserver(instanceId);
    return true;
}

export function beginTabDrag(instanceId, pointerEvent, payload) {
    return xlibDockLayoutBeginDrag(instanceId, pointerEvent, { ...payload, kind: 'tab' });
}

export function beginPanelDrag(instanceId, pointerEvent, payload) {
    return xlibDockLayoutBeginDrag(instanceId, pointerEvent, { ...payload, kind: 'panel' });
}

export function beginSplitterDrag(instanceId, pointerEvent, payload) {
    return xlibDockLayoutBeginDrag(instanceId, pointerEvent, { ...payload, kind: 'splitter' });
}

export function measure(instanceId, elementKeys) {
    const state = xlibDockLayoutInstances.get(instanceId);
    if (!state) return {};
    const result = {};
    for (const key of elementKeys ?? []) {
        const element = state.rootElement.querySelector(`[data-xdl-key="${CSS.escape(key)}"]`);
        if (!element) continue;
        result[key] = xlibDockLayoutRectToObject(element.getBoundingClientRect());
    }
    return result;
}

export function observeResize(instanceId, rootElement) {
    const state = xlibDockLayoutInstances.get(instanceId);
    if (!state || typeof ResizeObserver === 'undefined') return false;
    state.resizeObserver?.disconnect();
    state.resizeObserver = new ResizeObserver(entries => {
        const entry = entries[entries.length - 1];
        if (!entry) return;
        const { width, height } = entry.contentRect;
        if (Math.abs(width - state.lastResize.width) < 0.5 && Math.abs(height - state.lastResize.height) < 0.5) return;
        state.lastResize = { width, height };
        if (state.drag) xlibDockLayoutCancelDrag(state, 'resize');
        cancelAnimationFrame(state.rafId);
        state.rafId = requestAnimationFrame(() => {
            state.dotNetRef?.invokeMethodAsync('NotifyResizeAsync', width, height).catch(() => {});
        });
    });
    state.resizeObserver.observe(rootElement);
    return true;
}

export function setVisibilityObserver(instanceId) {
    const state = xlibDockLayoutInstances.get(instanceId);
    if (!state) return false;
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState !== 'visible') xlibDockLayoutCancelDrag(state, 'visibility-hidden');
    }, { signal: state.abortController.signal });
    return true;
}

export function saveLocalStorage(key, value) {
    localStorage.setItem(key, value);
}

export function loadLocalStorage(key) {
    return localStorage.getItem(key);
}

export function deleteLocalStorage(key) {
    localStorage.removeItem(key);
}

export function destroy(instanceId) {
    const state = xlibDockLayoutInstances.get(instanceId);
    if (!state) return false;
    xlibDockLayoutCancelDrag(state, 'destroy');
    state.abortController.abort();
    state.resizeObserver?.disconnect();
    cancelAnimationFrame(state.rafId);
    state.dotNetRef = null;
    state.rootElement = null;
    xlibDockLayoutInstances.delete(instanceId);
    return true;
}

function xlibDockLayoutHandlePointerDown(state, event) {
    if (event.button !== 0 || state.drag) return;

    if (event.target.closest?.('[data-xdl-no-drag],.xdl-tab-context-menu,.xdl-panel-move-menu')) return;

    const splitter = event.target.closest?.('[data-xdl-splitter]');
    if (splitter && splitter.dataset.xdlSplitter && !splitter.classList.contains('xdl-locked')) {
        const parent = splitter.closest('[data-xdl-editor-split]');
        const layoutTarget = parent ? null : xlibDockLayoutResolveLayoutSplitterTarget(state.rootElement, splitter.dataset.xdlSplitter);
        if (!parent && !layoutTarget) return;
        xlibDockLayoutBeginDrag(state.instanceId, event, {
            kind: parent ? 'splitter' : 'layoutSplitter',
            splitId: splitter.dataset.xdlSplitter,
            orientation: splitter.dataset.xdlOrientation,
            container: parent ?? layoutTarget.container,
            variableName: layoutTarget?.variableName,
            reverse: layoutTarget?.reverse ?? false
        });
        return;
    }

    const panelHeader = event.target.closest?.('[data-xdl-panel-header]');
    if (panelHeader && !event.target.closest('.xdl-panel-actions,button.xdl-icon-button,button.xdl-panel-move-option,[role="menu"]')) {
        const panel = panelHeader.closest('[data-xdl-panel]');
        xlibDockLayoutBeginDrag(state.instanceId, event, {
            kind: 'panel',
            panelId: panelHeader.dataset.xdlPanelHeader,
            title: panel?.getAttribute('aria-label') ?? panelHeader.dataset.xdlPanelHeader
        });
        return;
    }

    const tab = event.target.closest?.('[data-xdl-tab]');
    if (tab && !event.target.closest('.xdl-tab-pin,.xdl-tab-close,.xdl-tab-context-menu,[data-xdl-no-drag]')) {
        xlibDockLayoutBeginDrag(state.instanceId, event, {
            kind: 'tab',
            tabId: tab.dataset.xdlTab,
            sourceViewId: tab.dataset.xdlView,
            title: tab.getAttribute('title') ?? tab.dataset.xdlTab,
            sourceElement: tab
        });
    }
}

function xlibDockLayoutBeginDrag(instanceId, pointerEvent, payload) {
    const state = xlibDockLayoutInstances.get(instanceId);
    if (!state || !pointerEvent?.target || state.drag) return false;
    const handle = pointerEvent.target.closest?.('[data-xdl-tab],[data-xdl-panel-header],[data-xdl-splitter]') ?? pointerEvent.target;
    const pointerId = pointerEvent.pointerId;
    try { handle.setPointerCapture?.(pointerId); } catch { }
    state.drag = {
        pointerId,
        handle,
        payload,
        startX: pointerEvent.clientX,
        startY: pointerEvent.clientY,
        currentX: pointerEvent.clientX,
        currentY: pointerEvent.clientY,
        started: payload.kind === 'splitter' || payload.kind === 'layoutSplitter',
        target: null,
        preview: null,
        overlay: null,
        initialRatio: null
    };
    if (payload.kind === 'splitter' || payload.kind === 'layoutSplitter') xlibDockLayoutStartVisualDrag(state);
    return true;
}

function xlibDockLayoutHandlePointerMove(state, event) {
    const drag = state.drag;
    if (!drag || event.pointerId !== drag.pointerId) return;
    drag.currentX = event.clientX;
    drag.currentY = event.clientY;
    if (!drag.started) {
        const threshold = Math.max(1, Number(state.options.dragThreshold ?? state.options.DragThreshold ?? 4));
        if (Math.hypot(drag.currentX - drag.startX, drag.currentY - drag.startY) < threshold) return;
        drag.started = true;
        xlibDockLayoutStartVisualDrag(state);
    }
    event.preventDefault();
    cancelAnimationFrame(state.rafId);
    state.rafId = requestAnimationFrame(() => xlibDockLayoutUpdateVisualDrag(state));
}

function xlibDockLayoutStartVisualDrag(state) {
    const drag = state.drag;
    if (!drag) return;
    drag.handle.classList.add('xdl-dragging');
    if (drag.payload.kind !== 'splitter' && drag.payload.kind !== 'layoutSplitter') {
        const preview = document.createElement('div');
        preview.className = 'xdl-drag-preview';
        preview.textContent = drag.payload.title ?? drag.payload.tabId ?? drag.payload.panelId;
        state.rootElement.append(preview);
        drag.preview = preview;
    } else {
        const container = drag.payload.container;
        if (drag.payload.kind === 'splitter') {
            const rawRatio = getComputedStyle(container).getPropertyValue('--xdl-first-ratio').trim();
            drag.initialRatio = Number.parseFloat(rawRatio) / 100 || 0.5;
        } else {
            const rawRatio = getComputedStyle(state.rootElement).getPropertyValue(drag.payload.variableName).trim();
            drag.initialRatio = Number.parseFloat(rawRatio) / 100 || 0.5;
        }
    }
}

function xlibDockLayoutUpdateVisualDrag(state) {
    const drag = state.drag;
    if (!drag?.started) return;
    if (drag.preview) {
        const rootRect = state.rootElement.getBoundingClientRect();
        drag.preview.style.transform = `translate3d(${drag.currentX - rootRect.left + 12}px,${drag.currentY - rootRect.top + 12}px,0)`;
    }

    if (drag.payload.kind === 'tab') xlibDockLayoutUpdateTabTarget(state);
    else if (drag.payload.kind === 'panel') xlibDockLayoutUpdatePanelTarget(state);
    else if (drag.payload.kind === 'splitter') xlibDockLayoutUpdateSplitter(state);
    else xlibDockLayoutUpdateLayoutSplitter(state);
}

function xlibDockLayoutUpdateTabTarget(state) {
    const drag = state.drag;
    const view = xlibDockLayoutFindContaining(state.rootElement.querySelectorAll('[data-xdl-editor-view]'), drag.currentX, drag.currentY);
    xlibDockLayoutClearDropClasses(state);
    if (!view) {
        drag.target = null;
        return;
    }
    const rect = view.getBoundingClientRect();
    const x = (drag.currentX - rect.left) / Math.max(rect.width, 1);
    const y = (drag.currentY - rect.top) / Math.max(rect.height, 1);
    let direction = xlibDockLayoutDirection.Center;
    let directionName = 'center';
    if (x < 0.25) { direction = xlibDockLayoutDirection.Left; directionName = 'left'; }
    else if (x > 0.75) { direction = xlibDockLayoutDirection.Right; directionName = 'right'; }
    else if (y < 0.25) { direction = xlibDockLayoutDirection.Top; directionName = 'top'; }
    else if (y > 0.75) { direction = xlibDockLayoutDirection.Bottom; directionName = 'bottom'; }

    const targetIndex = direction === xlibDockLayoutDirection.Center
        ? xlibDockLayoutResolveTabIndex(view, drag.currentX)
        : -1;
    drag.target = { targetViewId: view.dataset.xdlEditorView, direction, targetIndex };
    view.classList.add('xdl-drop-target', `xdl-drop-${directionName}`);
    xlibDockLayoutCreateOverlay(drag, view, directionName);
}

function xlibDockLayoutUpdatePanelTarget(state) {
    const drag = state.drag;
    const region = xlibDockLayoutFindContaining(state.rootElement.querySelectorAll('[data-xdl-region]'), drag.currentX, drag.currentY);
    xlibDockLayoutClearDropClasses(state);
    if (!region || !(region.dataset.xdlRegion in xlibDockLayoutRegion)) {
        drag.target = null;
        return;
    }
    drag.target = {
        targetRegion: xlibDockLayoutRegion[region.dataset.xdlRegion],
        targetIndex: xlibDockLayoutResolvePanelIndex(region, drag.currentY)
    };
    region.classList.add('xdl-drop-target', 'xdl-drop-panel');
}

function xlibDockLayoutUpdateSplitter(state) {
    const drag = state.drag;
    const container = drag.payload.container;
    const rect = container.getBoundingClientRect();
    const vertical = drag.payload.orientation === 'vertical';
    const raw = vertical
        ? (drag.currentX - rect.left) / Math.max(rect.width, 1)
        : (drag.currentY - rect.top) / Math.max(rect.height, 1);
    const ratio = Math.min(0.95, Math.max(0.05, raw));
    container.style.setProperty('--xdl-first-ratio', `${ratio * 100}%`);
    drag.target = { firstRatio: ratio };
}

function xlibDockLayoutUpdateLayoutSplitter(state) {
    const drag = state.drag;
    const rect = drag.payload.container.getBoundingClientRect();
    const vertical = drag.payload.orientation === 'vertical';
    let raw = vertical
        ? (drag.currentX - rect.left) / Math.max(rect.width, 1)
        : (drag.currentY - rect.top) / Math.max(rect.height, 1);
    if (drag.payload.reverse) raw = 1 - raw;
    const max = drag.payload.splitId === 'layout-work-bottom' ? 0.70 : 0.95;
    const ratio = Math.min(max, Math.max(0.05, raw));
    state.rootElement.style.setProperty(drag.payload.variableName, `${ratio * 100}%`);
    drag.target = { ratio };
}

function xlibDockLayoutHandlePointerUp(state, event) {
    const drag = state.drag;
    if (!drag || event.pointerId !== drag.pointerId) return;
    if (!drag.started || !drag.target) {
        xlibDockLayoutCancelDrag(state, drag.started ? 'invalid-target' : 'click');
        return;
    }

    const target = drag.target;
    const payload = drag.payload;
    const dotNetRef = state.dotNetRef;
    xlibDockLayoutCleanupDragVisuals(state, true);
    state.drag = null;

    if (payload.kind === 'tab') {
        if (payload.sourceViewId === target.targetViewId && target.direction === xlibDockLayoutDirection.Center) {
            const sourceTabs = Array.from(state.rootElement.querySelectorAll(`[data-xdl-editor-view="${CSS.escape(payload.sourceViewId)}"] [data-xdl-tab]`));
            const sourceIndex = sourceTabs.findIndex(tab => tab.dataset.xdlTab === payload.tabId);
            if (target.targetIndex === sourceIndex || target.targetIndex === sourceIndex + 1) return;
        }
        dotNetRef?.invokeMethodAsync('CommitTabDragAsync', {
            tabId: payload.tabId,
            sourceViewId: payload.sourceViewId,
            targetViewId: target.targetViewId,
            direction: target.direction,
            targetIndex: target.targetIndex
        }).catch(() => {});
    } else if (payload.kind === 'panel') {
        dotNetRef?.invokeMethodAsync('CommitPanelDragAsync', {
            panelId: payload.panelId,
            targetRegion: target.targetRegion,
            targetIndex: target.targetIndex
        }).catch(() => {});
    } else if (payload.kind === 'splitter') {
        dotNetRef?.invokeMethodAsync('CommitSplitterDragAsync', {
            splitId: payload.splitId,
            firstRatio: target.firstRatio
        }).catch(() => {});
    } else {
        dotNetRef?.invokeMethodAsync('CommitLayoutSplitterDragAsync', {
            splitterId: payload.splitId,
            ratio: target.ratio
        }).catch(() => {});
    }
}

function xlibDockLayoutCancelDrag(state, reason) {
    const drag = state.drag;
    if (!drag) return;
    if (drag.payload.kind === 'splitter' && drag.initialRatio !== null) {
        drag.payload.container.style.setProperty('--xdl-first-ratio', `${drag.initialRatio * 100}%`);
    } else if (drag.payload.kind === 'layoutSplitter' && drag.initialRatio !== null) {
        state.rootElement.style.setProperty(drag.payload.variableName, `${drag.initialRatio * 100}%`);
    }
    xlibDockLayoutCleanupDragVisuals(state, true);
    state.drag = null;
    void reason;
}

function xlibDockLayoutCleanupDragVisuals(state, releaseCapture) {
    const drag = state.drag;
    if (!drag) return;
    if (releaseCapture) {
        try { drag.handle?.releasePointerCapture?.(drag.pointerId); } catch { }
    }
    cancelAnimationFrame(state.rafId);
    drag.preview?.remove();
    drag.overlay?.remove();
    state.rootElement?.querySelectorAll('.xdl-dragging,.xdl-drop-target,.xdl-drop-active').forEach(element => {
        element.classList.remove('xdl-dragging', 'xdl-drop-target', 'xdl-drop-active',
            'xdl-drop-left', 'xdl-drop-right', 'xdl-drop-top', 'xdl-drop-bottom', 'xdl-drop-center', 'xdl-drop-panel');
    });
}

function xlibDockLayoutCreateOverlay(drag, target, directionName) {
    drag.overlay?.remove();
    const overlay = document.createElement('div');
    overlay.className = `xdl-drop-preview xdl-drop-preview-${directionName}`;
    overlay.setAttribute('aria-hidden', 'true');
    target.append(overlay);
    drag.overlay = overlay;
}

function xlibDockLayoutClearDropClasses(state) {
    const drag = state.drag;
    drag?.overlay?.remove();
    if (drag) drag.overlay = null;
    state.rootElement.querySelectorAll('.xdl-drop-target').forEach(element => {
        element.classList.remove('xdl-drop-target', 'xdl-drop-left', 'xdl-drop-right', 'xdl-drop-top', 'xdl-drop-bottom', 'xdl-drop-center', 'xdl-drop-panel');
    });
}

function xlibDockLayoutFindContaining(elements, x, y) {
    for (const element of elements) {
        const rect = element.getBoundingClientRect();
        if (x >= rect.left && x <= rect.right && y >= rect.top && y <= rect.bottom) return element;
    }
    return null;
}

function xlibDockLayoutResolveTabIndex(view, x) {
    const tabs = Array.from(view.querySelectorAll(':scope > .xdl-tab-strip [data-xdl-tab]'));
    for (let index = 0; index < tabs.length; index++) {
        const rect = tabs[index].getBoundingClientRect();
        if (x < rect.left + rect.width / 2) return index;
    }
    return tabs.length;
}

function xlibDockLayoutResolvePanelIndex(region, y) {
    const panels = Array.from(region.querySelectorAll(':scope > [data-xdl-panel]'));
    for (let index = 0; index < panels.length; index++) {
        const rect = panels[index].getBoundingClientRect();
        if (y < rect.top + rect.height / 2) return index;
    }
    return panels.length;
}

function xlibDockLayoutResolveLayoutSplitterTarget(root, splitterId) {
    const mainRow = root.querySelector('.xdl-main-row');
    const workBody = root.querySelector('.xdl-work-body');
    const leftDock = root.querySelector('.xdl-side-dock-left');
    const rightDock = root.querySelector('.xdl-side-dock-right');
    const bottomDock = root.querySelector('.xdl-bottom-dock');
    const targets = {
        'layout-left-editor': { container: mainRow, variableName: '--xdl-left-size', reverse: false },
        'layout-editor-right': { container: mainRow, variableName: '--xdl-right-size', reverse: true },
        'layout-work-bottom': { container: workBody, variableName: '--xdl-bottom-size', reverse: true },
        'layout-left-regions': { container: leftDock, variableName: '--xdl-left-upper-size', reverse: false },
        'layout-right-regions': { container: rightDock, variableName: '--xdl-right-upper-size', reverse: false },
        'layout-bottom-regions': { container: bottomDock, variableName: '--xdl-bottom-left-size', reverse: false }
    };
    const target = targets[splitterId];
    return target?.container ? target : null;
}

function xlibDockLayoutHandleWheel(event) {
    const strip = event.target.closest?.('.xdl-tab-overflow-scroll');
    if (!strip || Math.abs(event.deltaY) <= Math.abs(event.deltaX)) return;
    strip.scrollLeft += event.deltaY;
    event.preventDefault();
}

function xlibDockLayoutRectToObject(rect) {
    return {
        x: rect.x,
        y: rect.y,
        width: rect.width,
        height: rect.height,
        top: rect.top,
        right: rect.right,
        bottom: rect.bottom,
        left: rect.left
    };
}
