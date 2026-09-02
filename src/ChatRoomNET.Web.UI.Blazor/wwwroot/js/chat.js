// Помощники для чата: скролл-логика infinite scroll (читать/писать scrollTop из Blazor нельзя без JS).

export function initScroll(container, dotnet) {
    const handler = () => {
        if (container.scrollTop < 40) {
            dotnet.invokeMethodAsync('OnScrolledToTop');
        }
    };
    container.addEventListener('scroll', handler);
    container._chatHandler = handler;
}

export function dispose(container) {
    if (container && container._chatHandler) {
        container.removeEventListener('scroll', container._chatHandler);
        container._chatHandler = null;
    }
}

export function scrollToBottom(container) {
    if (container) container.scrollTop = container.scrollHeight;
}

export function getScrollHeight(container) {
    return container ? container.scrollHeight : 0;
}

// После дозагрузки старых сообщений вверх сохраняем позицию: newHeight - prevHeight.
export function restoreScroll(container, prevHeight) {
    if (container) container.scrollTop = container.scrollHeight - prevHeight;
}
