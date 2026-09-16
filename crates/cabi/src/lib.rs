//! C ABI wrapper around the `ktav` Rust crate, consumed by the .NET
//! library via P/Invoke (LibraryImport).
//!
//! ## Wire format
//!
//! Between .NET and Rust we exchange **JSON**, not a custom binary. This
//! keeps the FFI boundary tiny and lets each side use its native JSON
//! machinery.
//!
//! Ktav typed scalars do not map 1:1 onto JSON numbers. To keep
//! round-trips lossless we use tagged wrappers:
//!
//! - `Value::Integer(s)` ⇄ `{"$i": "<digits>"}`
//! - `Value::Float(s)`   ⇄ `{"$f": "<text>"}`
//!
//! Everything else maps to the obvious JSON shape (`null`, booleans,
//! strings, arrays, objects). Object key order is preserved on both
//! sides (`indexmap` here, `System.Text.Json` on the .NET side).
//!
//! ## C ABI
//!
//! Eight functions; the six output functions all use the same
//! "caller-owned pointer, callee-owned buffer" pattern:
//!
//! - `ktav_loads(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_loads_strict(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_dumps(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_dumps_force_strings(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_emit_canonical(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_format(src, src_len, out_buf, out_len, out_err, out_err_len) -> i32`
//! - `ktav_free(ptr, len)` — free a buffer returned by loads/dumps/format.
//! - `ktav_version()` — NUL-terminated static string, for sanity checks.
//!
//! Return code: `0` on success, `1` on error. On error, `out_err` holds
//! the structured error envelope as UTF-8 JSON — one nine-field object
//! `error, reason, line, line_text, span, path, body, canonical,
//! spec_section` in that exact order, built by `ktav::ErrorEnvelope`
//! (issue rust#12). Both the success and the error buffer are freed via
//! `ktav_free`.

use std::os::raw::{c_char, c_int};
use std::ptr;
use std::slice;

use indexmap::IndexMap;
use ktav::value::{ObjectMap, Value};
use serde::de::{self, MapAccess, Visitor};
use serde::{Deserialize, Deserializer};
use serde_json::{Map as JsonMap, Value as Json};

/// Written into the caller's `**u8` / `*usize` on success.
#[inline]
unsafe fn emit(buf: Vec<u8>, out_buf: *mut *mut u8, out_len: *mut usize) {
    let mut boxed = buf.into_boxed_slice();
    let len = boxed.len();
    let ptr = boxed.as_mut_ptr();
    std::mem::forget(boxed);
    *out_buf = ptr;
    *out_len = len;
}

/// Ship the envelope JSON through the error channel as raw bytes.
#[inline]
unsafe fn emit_err(msg: String, out_err: *mut *mut c_char, out_err_len: *mut usize) {
    let bytes = msg.into_bytes();
    let mut boxed = bytes.into_boxed_slice();
    let len = boxed.len();
    let ptr = boxed.as_mut_ptr() as *mut c_char;
    std::mem::forget(boxed);
    *out_err = ptr;
    *out_err_len = len;
}

/// Ship `err` through the error channel as the structured envelope.
/// `source` is the Ktav document text the error refers to ("" when the
/// input was not Ktav text) so `line_text` can be populated.
unsafe fn emit_error(
    err: &ktav::Error,
    source: &str,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) {
    emit_err(
        ktav::ErrorEnvelope::from_error(err, source).to_json(),
        out_err,
        out_err_len,
    );
}

/// Uniformity rule (issue rust#12): every error leaving the ABI is an
/// envelope. Free-form text that carries no `ktav::Error` is wrapped as
/// `Error::Message` so hosts never have to sniff JSON vs plain text.
unsafe fn emit_message(
    message: String,
    source: &str,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) {
    emit_error(&ktav::Error::Message(message), source, out_err, out_err_len);
}

/// Parse a Ktav document. Returns JSON bytes on success, error envelope on
/// failure. Caller frees both via `ktav_free`.
///
/// # Safety
/// `src` must point to `src_len` valid bytes. Output pointers must be
/// valid for writes.
#[no_mangle]
pub unsafe extern "C" fn ktav_loads(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let input = match std::str::from_utf8(slice::from_raw_parts(src, src_len)) {
        Ok(s) => s,
        Err(e) => {
            // We refused to decode the bytes; deriving line_text from
            // them would be dishonest, so no source is attached.
            emit_message(
                format!("input is not valid UTF-8: {e}"),
                "",
                out_err,
                out_err_len,
            );
            return 1;
        }
    };

    let value = match ktav::parse(input) {
        Ok(v) => v,
        Err(e) => {
            emit_error(&e, input, out_err, out_err_len);
            return 1;
        }
    };

    let json = value_to_json(&value);
    let bytes = match serde_json::to_vec(&json) {
        Ok(b) => b,
        Err(e) => {
            emit_message(
                format!("internal: encode JSON: {e}"),
                input,
                out_err,
                out_err_len,
            );
            return 1;
        }
    };

    emit(bytes, out_buf, out_len);
    0
}

/// Parse a Ktav document with strict numeric spelling checks. Returns JSON
/// bytes on success, error envelope on failure. Caller frees both via
/// `ktav_free`.
///
/// # Safety
/// Same as [`ktav_loads`].
#[no_mangle]
pub unsafe extern "C" fn ktav_loads_strict(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let input = match std::str::from_utf8(slice::from_raw_parts(src, src_len)) {
        Ok(s) => s,
        Err(e) => {
            emit_message(
                format!("input is not valid UTF-8: {e}"),
                "",
                out_err,
                out_err_len,
            );
            return 1;
        }
    };

    let value = match ktav::parse_strict(input) {
        Ok(v) => v,
        Err(e) => {
            emit_error(&e, input, out_err, out_err_len);
            return 1;
        }
    };

    let json = value_to_json(&value);
    let bytes = match serde_json::to_vec(&json) {
        Ok(b) => b,
        Err(e) => {
            emit_message(
                format!("internal: encode JSON: {e}"),
                input,
                out_err,
                out_err_len,
            );
            return 1;
        }
    };

    emit(bytes, out_buf, out_len);
    0
}

/// Render a JSON document (as produced by `ktav_loads` or built by the
/// caller to the same schema) to Ktav text.
///
/// # Safety
/// Same as [`ktav_loads`].
#[no_mangle]
pub unsafe extern "C" fn ktav_dumps(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let bytes = slice::from_raw_parts(src, src_len);
    let wire: WireValue = match serde_json::from_slice(bytes) {
        Ok(w) => w,
        Err(e) => {
            emit_message(format!("input JSON: {e}"), "", out_err, out_err_len);
            return 1;
        }
    };

    let value = match wire.into_value() {
        Ok(v) => v,
        Err(e) => {
            emit_message(e, "", out_err, out_err_len);
            return 1;
        }
    };

    if !matches!(value, Value::Object(_) | Value::Array(_)) {
        emit_message(
            "top-level Ktav document must be an object or array".to_string(),
            "",
            out_err,
            out_err_len,
        );
        return 1;
    }

    let text = match ktav::render::render(&value) {
        Ok(s) => s,
        Err(e) => {
            emit_error(&e, "", out_err, out_err_len);
            return 1;
        }
    };

    emit(text.into_bytes(), out_buf, out_len);
    0
}

/// Render a JSON document to Ktav text with **every scalar coerced to
/// a String** — typed integers (`:i`), typed floats (`:f`), booleans,
/// and null are flattened to their textual form via the raw-marker
/// `::`. Compounds preserve their structure. The output round-trips
/// back through `ktav_loads` as the same set of String scalars.
///
/// # Safety
/// Same as [`ktav_loads`].
#[no_mangle]
pub unsafe extern "C" fn ktav_dumps_force_strings(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let bytes = slice::from_raw_parts(src, src_len);
    let wire: WireValue = match serde_json::from_slice(bytes) {
        Ok(w) => w,
        Err(e) => {
            emit_message(format!("input JSON: {e}"), "", out_err, out_err_len);
            return 1;
        }
    };

    let value = match wire.into_value() {
        Ok(v) => v,
        Err(e) => {
            emit_message(e, "", out_err, out_err_len);
            return 1;
        }
    };

    if !matches!(value, Value::Object(_) | Value::Array(_)) {
        emit_message(
            "top-level Ktav document must be an object or array".to_string(),
            "",
            out_err,
            out_err_len,
        );
        return 1;
    }

    let text = match ktav::to_string_force_strings(&value) {
        Ok(s) => s,
        Err(e) => {
            emit_error(&e, "", out_err, out_err_len);
            return 1;
        }
    };

    emit(text.into_bytes(), out_buf, out_len);
    0
}

/// Render a Ktav document to its **canonical (normalised) form** as
/// defined by spec § 5.9. The input is JSON (same schema as accepted by
/// `ktav_dumps`); the output is canonical Ktav text. Parsing the output
/// and calling `ktav_emit_canonical` again yields byte-identical output
/// (idempotent).
///
/// # Safety
/// Same as [`ktav_loads`].
#[no_mangle]
pub unsafe extern "C" fn ktav_emit_canonical(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let bytes = slice::from_raw_parts(src, src_len);
    let wire: WireValue = match serde_json::from_slice(bytes) {
        Ok(w) => w,
        Err(e) => {
            emit_message(format!("input JSON: {e}"), "", out_err, out_err_len);
            return 1;
        }
    };

    let value = match wire.into_value() {
        Ok(v) => v,
        Err(e) => {
            emit_message(e, "", out_err, out_err_len);
            return 1;
        }
    };

    if !matches!(value, Value::Object(_) | Value::Array(_)) {
        emit_message(
            "top-level Ktav document must be an object or array".to_string(),
            "",
            out_err,
            out_err_len,
        );
        return 1;
    }

    let text = match ktav::emit_canonical(&value) {
        Ok(s) => s,
        Err(e) => {
            emit_error(&e, "", out_err, out_err_len);
            return 1;
        }
    };

    emit(text.into_bytes(), out_buf, out_len);
    0
}

/// Format Ktav **source text** (not JSON — contrast with
/// `ktav_emit_canonical`, which takes the JSON wire value; no
/// deserialization happens here) into its canonical layout.
///
/// Semantics (settled upstream):
/// - every comment is preserved verbatim (spec § 3.4: a comment owns a
///   whole line, so attachment is unambiguous);
/// - blank lines survive as a grouping hint, but a run of two or more
///   collapses to exactly one, and blank padding immediately inside a
///   bracket is dropped — which makes the transform a fixed point
///   (`format(format(x)) == format(x)`);
/// - key order is never changed (§ 5.9 has no sorting rule);
/// - for a document with no comments AND no blank lines the result
///   equals `ktav_emit_canonical` of its parse.
///
/// Both the success and the error buffer are freed via `ktav_free`.
///
/// # Safety
/// Same as [`ktav_loads`].
#[no_mangle]
pub unsafe extern "C" fn ktav_format(
    src: *const u8,
    src_len: usize,
    out_buf: *mut *mut u8,
    out_len: *mut usize,
    out_err: *mut *mut c_char,
    out_err_len: *mut usize,
) -> c_int {
    *out_buf = ptr::null_mut();
    *out_len = 0;
    *out_err = ptr::null_mut();
    *out_err_len = 0;

    let text = match std::str::from_utf8(slice::from_raw_parts(src, src_len)) {
        Ok(s) => s,
        Err(e) => {
            emit_message(
                format!("input is not valid UTF-8: {e}"),
                "",
                out_err,
                out_err_len,
            );
            return 1;
        }
    };

    match ktav::format_str(text) {
        Ok(formatted) => {
            emit(formatted.into_bytes(), out_buf, out_len);
            0
        }
        Err(e) => {
            emit_error(&e, text, out_err, out_err_len);
            1
        }
    }
}

/// Free a buffer returned by any of the output functions — `ktav_loads`,
/// `ktav_loads_strict`, `ktav_dumps`, `ktav_dumps_force_strings`,
/// `ktav_emit_canonical`, and `ktav_format` — success or error.
/// `ptr`/`len` is a no-op when null/zero.
///
/// # Safety
/// Must be called exactly once per returned buffer with the same length
/// it was returned with.
#[no_mangle]
pub unsafe extern "C" fn ktav_free(ptr: *mut u8, len: usize) {
    if ptr.is_null() || len == 0 {
        return;
    }
    let _ = Box::from_raw(std::ptr::slice_from_raw_parts_mut(ptr, len));
}

/// NUL-terminated static version string (crate version). For sanity
/// checks from the Go side that `LoadLibrary` picked up the right file.
#[no_mangle]
pub extern "C" fn ktav_version() -> *const c_char {
    concat!(env!("CARGO_PKG_VERSION"), "\0").as_ptr() as *const c_char
}

// ─── Value ↔ JSON conversion ──────────────────────────────────────────────

fn value_to_json(v: &Value) -> Json {
    match v {
        Value::Null => Json::Null,
        Value::Bool(b) => Json::Bool(*b),
        Value::Integer(s) => {
            let mut m = JsonMap::new();
            m.insert("$i".to_string(), Json::String(s.to_string()));
            Json::Object(m)
        }
        Value::Float(s) => {
            let mut m = JsonMap::new();
            m.insert("$f".to_string(), Json::String(s.to_string()));
            Json::Object(m)
        }
        Value::String(s) => Json::String(s.to_string()),
        Value::Array(a) => Json::Array(a.iter().map(value_to_json).collect()),
        Value::Object(o) => {
            let mut m = JsonMap::new();
            for (k, val) in o {
                m.insert(k.to_string(), value_to_json(val));
            }
            Json::Object(m)
        }
    }
}

/// Deserialize target that understands both plain JSON values and the
/// `{"$i": ...}` / `{"$f": ...}` tagged wrappers, preserving object key
/// order via `indexmap`.
enum WireValue {
    Null,
    Bool(bool),
    Integer(String),
    Float(String),
    String(String),
    Array(Vec<WireValue>),
    Object(IndexMap<String, WireValue>),
}

impl WireValue {
    fn into_value(self) -> Result<Value, String> {
        match self {
            WireValue::Null => Ok(Value::Null),
            WireValue::Bool(b) => Ok(Value::Bool(b)),
            WireValue::Integer(s) => {
                validate_integer(&s)?;
                Ok(Value::Integer(s.into()))
            }
            WireValue::Float(s) => {
                validate_float(&s)?;
                Ok(Value::Float(s.into()))
            }
            WireValue::String(s) => Ok(Value::String(s.into())),
            WireValue::Array(items) => {
                let mut out = Vec::with_capacity(items.len());
                for w in items {
                    out.push(w.into_value()?);
                }
                Ok(Value::Array(out))
            }
            WireValue::Object(m) => {
                let mut obj = ObjectMap::with_capacity_and_hasher(m.len(), Default::default());
                for (k, v) in m {
                    obj.insert(k.into(), v.into_value()?);
                }
                Ok(Value::Object(obj))
            }
        }
    }
}

fn validate_integer(s: &str) -> Result<(), String> {
    let rest = s.strip_prefix('-').unwrap_or(s);
    if rest.is_empty() || !rest.bytes().all(|b| b.is_ascii_digit()) {
        return Err(format!("$i payload not an integer literal: {s:?}"));
    }
    Ok(())
}

fn validate_float(s: &str) -> Result<(), String> {
    if s.parse::<f64>().is_err() {
        return Err(format!("$f payload not a finite decimal: {s:?}"));
    }
    if !s.bytes().any(|b| b == b'.' || b == b'e' || b == b'E') {
        return Err(format!("$f payload must contain '.' or exponent: {s:?}"));
    }
    Ok(())
}

impl<'de> Deserialize<'de> for WireValue {
    fn deserialize<D: Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        struct V;
        impl<'de> Visitor<'de> for V {
            type Value = WireValue;
            fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                f.write_str("a JSON value")
            }
            fn visit_unit<E: de::Error>(self) -> Result<WireValue, E> {
                Ok(WireValue::Null)
            }
            fn visit_none<E: de::Error>(self) -> Result<WireValue, E> {
                Ok(WireValue::Null)
            }
            fn visit_some<D: Deserializer<'de>>(self, d: D) -> Result<WireValue, D::Error> {
                WireValue::deserialize(d)
            }
            fn visit_bool<E: de::Error>(self, b: bool) -> Result<WireValue, E> {
                Ok(WireValue::Bool(b))
            }
            fn visit_i64<E: de::Error>(self, n: i64) -> Result<WireValue, E> {
                Ok(WireValue::Integer(n.to_string()))
            }
            fn visit_u64<E: de::Error>(self, n: u64) -> Result<WireValue, E> {
                Ok(WireValue::Integer(n.to_string()))
            }
            fn visit_f64<E: de::Error>(self, n: f64) -> Result<WireValue, E> {
                if !n.is_finite() {
                    return Err(E::custom("NaN / ±Infinity not allowed in Ktav"));
                }
                // Bare JSON floats get the ":f" wire form with a forced
                // decimal point so render's grammar check is satisfied.
                let mut s = format!("{n}");
                if !s.contains('.') && !s.contains('e') && !s.contains('E') {
                    s.push_str(".0");
                }
                Ok(WireValue::Float(s))
            }
            fn visit_str<E: de::Error>(self, v: &str) -> Result<WireValue, E> {
                Ok(WireValue::String(v.to_string()))
            }
            fn visit_string<E: de::Error>(self, v: String) -> Result<WireValue, E> {
                Ok(WireValue::String(v))
            }
            fn visit_seq<A: de::SeqAccess<'de>>(self, mut seq: A) -> Result<WireValue, A::Error> {
                let mut out = Vec::new();
                while let Some(item) = seq.next_element()? {
                    out.push(item);
                }
                Ok(WireValue::Array(out))
            }
            fn visit_map<A: MapAccess<'de>>(self, mut map: A) -> Result<WireValue, A::Error> {
                let Some(k1) = map.next_key::<String>()? else {
                    return Ok(WireValue::Object(IndexMap::new()));
                };
                let v1: WireValue = map.next_value()?;
                let second_key: Option<String> = map.next_key()?;

                if second_key.is_none() && (k1 == "$i" || k1 == "$f") {
                    let payload = match v1 {
                        WireValue::String(s) => s,
                        WireValue::Integer(s) => s,
                        WireValue::Float(s) => s,
                        _ => {
                            return Err(de::Error::custom(format!("{k1} payload must be a string")))
                        }
                    };
                    return Ok(if k1 == "$i" {
                        WireValue::Integer(payload)
                    } else {
                        WireValue::Float(payload)
                    });
                }

                let mut out: IndexMap<String, WireValue> = IndexMap::new();
                out.insert(k1, v1);
                if let Some(k2) = second_key {
                    let v2: WireValue = map.next_value()?;
                    out.insert(k2, v2);
                    while let Some((k, v)) = map.next_entry::<String, WireValue>()? {
                        out.insert(k, v);
                    }
                }
                Ok(WireValue::Object(out))
            }
        }

        d.deserialize_any(V)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    type AbiFn = unsafe extern "C" fn(
        *const u8,
        usize,
        *mut *mut u8,
        *mut usize,
        *mut *mut c_char,
        *mut usize,
    ) -> c_int;

    struct Raw {
        rc: c_int,
        out: Option<String>,
        err: Option<String>,
    }

    /// SAFETY: `f` is one of this crate's ABI functions; `bytes` lives
    /// for the whole call; all out pointers are valid locals; returned
    /// buffers are copied out and freed via `ktav_free` before returning.
    fn call_raw(f: AbiFn, bytes: &[u8]) -> Raw {
        let mut out_buf: *mut u8 = ptr::null_mut();
        let mut out_len: usize = 0;
        let mut out_err: *mut c_char = ptr::null_mut();
        let mut out_err_len: usize = 0;
        // SAFETY: `f` is one of this crate's ABI functions; `bytes`
        // outlives the call; all out pointers are valid locals.
        let rc = unsafe {
            f(
                bytes.as_ptr(),
                bytes.len(),
                &mut out_buf,
                &mut out_len,
                &mut out_err,
                &mut out_err_len,
            )
        };
        let out = if !out_buf.is_null() {
            // SAFETY: the callee allocated exactly out_len bytes.
            Some(unsafe {
                let s = String::from_utf8(slice::from_raw_parts(out_buf, out_len).to_vec())
                    .expect("output is UTF-8");
                ktav_free(out_buf, out_len);
                s
            })
        } else {
            assert_eq!(out_len, 0);
            None
        };
        let err = if !out_err.is_null() {
            // SAFETY: the callee allocated exactly out_err_len bytes.
            Some(unsafe {
                let s = String::from_utf8(
                    slice::from_raw_parts(out_err as *const u8, out_err_len).to_vec(),
                )
                .expect("error payload is UTF-8");
                ktav_free(out_err as *mut u8, out_err_len);
                s
            })
        } else {
            assert_eq!(out_err_len, 0);
            None
        };
        Raw { rc, out, err }
    }

    fn call(f: AbiFn, bytes: &[u8]) -> Result<String, String> {
        let raw = call_raw(f, bytes);
        if raw.rc == 0 {
            Ok(raw.out.expect("success must fill out_buf"))
        } else {
            assert!(raw.out.is_none(), "out_buf must stay null on error");
            Err(raw.err.expect("error must fill out_err"))
        }
    }

    fn envelope(msg: &str) -> serde_json::Map<String, Json> {
        match serde_json::from_str::<Json>(msg) {
            Ok(Json::Object(m)) => m,
            other => panic!("error payload is not a JSON object: {other:?}"),
        }
    }

    #[test]
    fn format_keeps_comments_and_is_fixed_point() {
        let doc = "## header comment\n\
                   ## second comment\n\
                   \n\
                   \n\
                   \n\
                   alpha: 1\n\
                   beta: [\n\
                   \n\
                   inner: 2\n\
                   other: 3\n\
                   ]\n\
                   gamma: -5.5\n\
                   delta: -7\n\
                   ## trailing comment\n";
        // SAFETY: pure test call into the ABI with valid locals.
        let once = call(ktav_format, doc.as_bytes()).expect("format succeeds");
        for c in [
            "## header comment",
            "## second comment",
            "## trailing comment",
        ] {
            assert!(
                once.lines().any(|l| l == c),
                "comment {c:?} missing from output"
            );
        }
        assert!(
            !once.contains("\n\n\n"),
            "blank run must collapse: {once:?}"
        );
        // SAFETY: same as above, on previously formatted output.
        let twice = call(ktav_format, once.as_bytes()).expect("re-format succeeds");
        assert_eq!(once, twice, "format must be a fixed point");
    }

    #[test]
    fn format_matches_canonical_when_no_comments_or_blanks() {
        let text = "outer.inner: 1\n\
                    outer.list: [2, 3]\n\
                    inline: [a: x, b: y]\n\
                    neg: -42\n\
                    fl: 2.5\n";
        // SAFETY: pure test call into the ABI with valid locals.
        let via_abi = call(ktav_format, text.as_bytes()).expect("format succeeds");
        let canonical = ktav::emit_canonical(&ktav::parse(text).unwrap()).unwrap();
        assert_eq!(via_abi, canonical);
    }

    #[test]
    fn format_error_surfaces_structured_envelope() {
        // SAFETY: pure test call into the ABI with valid locals.
        let raw = call_raw(ktav_format, b"a: [");
        assert_eq!(raw.rc, 1);
        assert!(raw.out.is_none(), "out_buf must stay null on error");
        let env = envelope(raw.err.as_deref().expect("error payload"));
        let keys: Vec<&str> = env.keys().map(String::as_str).collect();
        assert_eq!(
            keys,
            [
                "error",
                "reason",
                "line",
                "line_text",
                "span",
                "path",
                "body",
                "canonical",
                "spec_section"
            ]
        );
        assert!(!env["error"].as_str().unwrap().is_empty());
        // Observed behaviour: for an unclosed inline compound the
        // envelope carries the span but `line` stays null.
        assert!(env["line"].is_null());
        assert_eq!(env["line_text"], "a: [");
        let span = env["span"].as_object().expect("span is an object");
        assert!(span["start"].is_number() && span["end"].is_number());
        assert!(env["reason"].is_null());
        assert!(env["path"].is_null());
    }

    #[test]
    fn loads_error_is_envelope_too() {
        // SAFETY: pure test call into the ABI with valid locals.
        let msg = call(ktav_loads, b"a: [").expect_err("must fail");
        let env = envelope(&msg);
        assert!(env.contains_key("error"), "not an envelope: {msg:?}");
    }

    #[test]
    fn message_errors_come_out_as_envelope() {
        // SAFETY: pure test call into the ABI with valid locals.
        let msg = call(ktav_dumps, b"{not json").expect_err("must fail");
        let env = envelope(&msg);
        assert_eq!(env["error"], "Message");
    }

    #[test]
    fn format_rejects_invalid_utf8_as_envelope() {
        // SAFETY: pure test call into the ABI with valid locals.
        let msg = call(ktav_format, &[0xFF, 0xFE]).expect_err("must fail");
        let env = envelope(&msg);
        assert_eq!(env["error"], "Message");
        assert!(env["line"].is_null());
    }
}
