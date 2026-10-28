use serde::Serialize;

/// ImplResourcePack passes this flag when its "Auto open/close ImplDmNote"
/// option opens the app together with ADOFAI. Such an instance starts in the
/// background and quits once the game is gone.
pub const LAUNCHED_BY_GAME_ARG: &str = "--launched-by-game";

/// How this process was started; managed as Tauri state.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LaunchContext {
    pub launched_by_game: bool,
}

impl LaunchContext {
    pub fn from_args<I, S>(args: I) -> Self
    where
        I: IntoIterator<Item = S>,
        S: AsRef<str>,
    {
        Self {
            launched_by_game: args
                .into_iter()
                .any(|arg| arg.as_ref() == LAUNCHED_BY_GAME_ARG),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn detects_the_game_flag_anywhere() {
        assert!(LaunchContext::from_args(["impl-dm-note", LAUNCHED_BY_GAME_ARG]).launched_by_game);
        assert!(!LaunchContext::from_args(["impl-dm-note"]).launched_by_game);
        assert!(
            !LaunchContext::from_args(["impl-dm-note", "--launched-by-game=1"]).launched_by_game
        );
    }
}
