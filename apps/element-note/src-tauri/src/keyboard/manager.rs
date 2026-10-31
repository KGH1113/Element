use std::sync::Arc;

use parking_lot::RwLock;

use crate::models::KeyMappings;

#[derive(Clone)]
pub struct KeyboardManager {
    mappings: Arc<RwLock<KeyMappings>>,
    current_mode: Arc<RwLock<String>>,
}

impl KeyboardManager {
    pub fn new(initial: KeyMappings, default_mode: impl Into<String>) -> Self {
        let mappings = Arc::new(RwLock::new(initial));
        let current_mode = Arc::new(RwLock::new(default_mode.into()));
        Self {
            mappings,
            current_mode,
        }
    }

    pub fn update_mappings(&self, mappings: KeyMappings) {
        *self.mappings.write() = mappings;
    }

    pub fn set_mode(&self, mode: impl Into<String>) -> bool {
        let mode = mode.into();
        let exists = self.mappings.read().contains_key(&mode);
        if exists {
            *self.current_mode.write() = mode;
        }
        exists
    }

    pub fn current_mode(&self) -> String {
        self.current_mode.read().clone()
    }
}
