import { obsApi } from '@api/modules/obsApi';
import type { ObsStatus } from '@src/types/obs';

/** Starts or stops the OBS server and keeps the choice for the next launch. */
export async function setObsModeRunning(running: boolean): Promise<ObsStatus> {
  const status = running ? await obsApi.start() : await obsApi.stop();
  await window.api.settings.update({ obsModeEnabled: running });
  return status;
}
