//! Thin wrapper crate: the whole exported C ABI surface comes from the
//! [`ktav::declare_cabi!`] macro (the `cabi` feature of the `ktav`
//! dependency), which expands the same `#[no_mangle] extern "C"`
//! symbols this crate used to hand-write. See the macro documentation
//! in the `ktav` crate for the JSON wire format, the
//! `ktav_free(ptr, len)` ownership contract, and the JSON error
//! envelope.
//!
//! Note: `ktav_version()` reports the `ktav` crate version, and
//! `ktav_abi_version()` reports the ABI shape version to compare
//! against before loading.

ktav::declare_cabi!();
