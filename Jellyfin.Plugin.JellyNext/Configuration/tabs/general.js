// General tab initialization and logic

function initGeneralTab() {
    // General tab has no specific initialization logic
    // All inputs are handled by the global form load/save logic
    console.log('General tab initialized');
}

function loadGeneralSettings(config) {
    document.getElementById('DiscoveryCustomCss').value = config.DiscoveryCustomCss || '';
    const selectedUsers = new Set((config.CatalogRequestUsers || []).map(id => id.replaceAll('-', '').toLowerCase()));
    document.querySelectorAll('#CatalogRequestUsers input').forEach(input => {
        input.checked = selectedUsers.has(input.value.replaceAll('-', '').toLowerCase());
    });
    document.getElementById('CacheExpirationHours').value = config.CacheExpirationHours || 6;
    document.getElementById('UseShortDummyVideo').checked = config.UseShortDummyVideo !== false;
    document.getElementById('PlaybackStopDelaySeconds').value = config.PlaybackStopDelaySeconds || 2;
}

function saveGeneralSettings(config) {
    config.DiscoveryCustomCss = document.getElementById('DiscoveryCustomCss').value;
    config.CatalogRequestUsers = Array.from(document.querySelectorAll('#CatalogRequestUsers input:checked'), input => input.value);
    config.CacheExpirationHours = parseInt(document.getElementById('CacheExpirationHours').value, 10);
    config.UseShortDummyVideo = document.getElementById('UseShortDummyVideo').checked;
    config.PlaybackStopDelaySeconds = parseInt(document.getElementById('PlaybackStopDelaySeconds').value, 10);
}
