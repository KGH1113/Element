import '@styles/viewer-compat-v1.css';
import '@styles/tokens.css';
import '@styles/chrome-global.css';
import '@styles/chrome-main.css';
import './visual.css';

const root = document.getElementById('root');
if (!root) throw new Error('Style scope fixture root not found');

const app = document.createElement('main');
app.dataset.dmnAppChrome = '';
app.innerHTML = `
  <label id="app-label">Application chrome<input id="app-control" /></label>
  <section data-dmn-viewer-surface>
    <label id="viewer-label">Viewer surface<input id="viewer-control" /></label>
  </section>
`;
root.appendChild(app);

const portal = document.createElement('aside');
portal.dataset.dmnAppPortal = '';
portal.innerHTML = '<button id="portal-control" type="button">Portal</button>';
document.body.appendChild(portal);

document.fonts.ready.then(() => {
  window.__VISUAL_READY__ = true;
});
