(function () {
  try {
    if (!sessionStorage.getItem('te-tab-id')) {
      sessionStorage.setItem('te-tab-id', 't_' + Date.now() + '_' + Math.random().toString(36).slice(2, 10));
    }
  } catch (e) { /* ignore */ }

  var nav = performance.getEntriesByType && performance.getEntriesByType('navigation')[0];
  if (nav && nav.type === 'reload') {
    window.__dataResetPromise = fetch('/api/session/reload', {
      method: 'POST',
      credentials: 'include',
      keepalive: true
    });
  }
})();

window.teTabHeaders = function () {
  var headers = { 'Content-Type': 'application/json' };
  try {
    var tabId = sessionStorage.getItem('te-tab-id');
    if (tabId) headers['X-Te-Tab-Id'] = tabId;
  } catch (e) { /* ignore */ }
  return headers;
};
