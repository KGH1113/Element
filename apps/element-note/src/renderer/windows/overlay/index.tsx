import '@api/dmnoteApi';
import React from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@contexts/I18nContext';
import '@styles/viewer-compat-v1.css';

window.__dmn_window_type = 'overlay';

async function bootstrap() {
  try {
    const { default: App } = await import('./App');
    const container = document.getElementById('root')!;
    container.dataset.dmnViewerSurface = '';
    const root = createRoot(container);
    root.render(
      <I18nProvider>
        <App />
      </I18nProvider>,
    );
  } catch (error) {
    const err = error as Error;
    console.error('[Overlay] Failed to mount React app:', err);
    document.body.innerHTML = `<pre style="color: red; padding: 20px;">Overlay Error: ${err.message}\n${err.stack}</pre>`;
  }
}

bootstrap();
