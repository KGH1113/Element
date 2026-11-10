//! Key counts while TUFReplay replays a run in ADOFAI.
//!
//! A replay presses keys through the OS, so ElementNote sees them like the player's own.
//! While Element reports a replay, presses count in a separate set that starts from the
//! counts the run was recorded at, and the windows show that set. The player's own
//! `key_counters` stay untouched, so replayed presses never reach the saved totals.
//!
//! Lock order: `key_counters` before `replay_key_counters`, as everywhere else.

use super::*;

impl AppState {
    /// Starts counting a replay. `recorded` holds the counts the run was recorded at; slots it
    /// lacks, and every slot when it is `None`, start from the player's current counts.
    pub(crate) fn begin_replay_key_counters(
        &self,
        emitter: &dyn KeyCounterEventEmitter,
        recorded: Option<KeyCounters>,
    ) {
        let counters = self.key_counters.write();
        let mut shown = counters.clone();
        for (mode, keys) in recorded.unwrap_or_default() {
            let entry = shown.entry(mode).or_default();
            for (key, count) in keys {
                entry.insert(key, count);
            }
        }
        *self.replay_key_counters.lock() = Some(shown.clone());
        self.replay_key_counting.store(true, Ordering::Release);
        let revision = self.next_key_counters_revision();
        if let Err(error) =
            emitter.emit_key_counters(&shown, &self.key_counters_session_id, revision)
        {
            error!("failed to emit replay key counters: {error:#}");
        }
    }

    /// Stops counting a replay and shows the player's own counts again. False when no
    /// replay was being counted.
    pub(crate) fn end_replay_key_counters(&self, emitter: &dyn KeyCounterEventEmitter) -> bool {
        let counters = self.key_counters.write();
        self.replay_key_counting.store(false, Ordering::Release);
        if self.replay_key_counters.lock().take().is_none() {
            return false;
        }
        let revision = self.next_key_counters_revision();
        if let Err(error) =
            emitter.emit_key_counters(&counters, &self.key_counters_session_id, revision)
        {
            error!("failed to emit key counters after a replay: {error:#}");
        }
        true
    }

    /// Counts a press in the replay's set. `None` when no replay is counted, so the press
    /// counts as the player's own.
    pub(super) fn increment_replay_key_counter_and_emit(
        &self,
        emitter: &dyn KeyCounterEventEmitter,
        mode: &str,
        key: &str,
    ) -> Option<u32> {
        // Outside replays this is one atomic load per press.
        if !self.replay_key_counting.load(Ordering::Acquire) {
            return None;
        }
        // Held so this event's revision is ordered with every other counter event.
        let _counters = self.key_counters.write();
        let mut replay = self.replay_key_counters.lock();
        let counters = replay.as_mut()?;
        let count = counters
            .entry(mode.to_string())
            .or_default()
            .entry(key.to_string())
            .or_insert(0);
        *count = count.saturating_add(1);
        let count = *count;
        let revision = self.next_key_counters_revision();
        if let Err(error) =
            emitter.emit_key_counter(mode, key, count, &self.key_counters_session_id, revision)
        {
            error!("failed to emit replay keys:counter event: {error:#}");
        }
        Some(count)
    }

    /// The counts the windows show: the replay's while one is counted, otherwise the player's.
    pub fn displayed_key_counters(&self) -> KeyCounters {
        let counters = self.key_counters.read();
        self.shown_key_counters_locked(&counters)
    }

    /// Like [`Self::displayed_key_counters`] for a caller already holding `key_counters`.
    pub(super) fn shown_key_counters_locked(&self, counters: &KeyCounters) -> KeyCounters {
        match self.replay_key_counters.lock().as_ref() {
            Some(replay) => replay.clone(),
            None => counters.clone(),
        }
    }

    /// Emits the player's counters after they changed, or the replay's while one is counted,
    /// so a reset or an edit during a replay shows up once the replay ends. Call with
    /// `key_counters` held.
    pub(super) fn emit_shown_key_counters(
        &self,
        emitter: &dyn KeyCounterEventEmitter,
        counters: &KeyCounters,
        revision: u64,
    ) -> Result<()> {
        match self.replay_key_counters.lock().as_ref() {
            Some(replay) => {
                emitter.emit_key_counters(replay, &self.key_counters_session_id, revision)
            }
            None => emitter.emit_key_counters(counters, &self.key_counters_session_id, revision),
        }
    }
}
