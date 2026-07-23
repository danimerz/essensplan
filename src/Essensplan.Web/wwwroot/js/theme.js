// Theme handling: persists an explicit light/dark choice in localStorage,
// falling back to the OS preference when the user hasn't chosen yet.
// The initial application (before first paint) happens via an inline
// script in App.razor to avoid a flash of the wrong theme; this module
// exposes the same logic for the interactive toggle component.

const STORAGE_KEY = 'essensplan-theme';

function systemPrefersDark() {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function getStoredTheme() {
    return localStorage.getItem(STORAGE_KEY);
}

function getEffectiveTheme() {
    return getStoredTheme() ?? (systemPrefersDark() ? 'dark' : 'light');
}

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
}

window.essensplanTheme = {
    get: () => getEffectiveTheme(),
    set: (theme) => {
        localStorage.setItem(STORAGE_KEY, theme);
        applyTheme(theme);
        return theme;
    },
    toggle: () => {
        const next = getEffectiveTheme() === 'dark' ? 'light' : 'dark';
        localStorage.setItem(STORAGE_KEY, next);
        applyTheme(next);
        return next;
    }
};
