// Copyright 2026 Aaron R Robinson
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

//! Example Rust application that consumes .NET exports via DNNE.
//!
//! Build and run:
//!   1. Build the ExportingAssembly with Rust output:
//!        dotnet build ../ExportingAssembly -p:DnneLanguage=rust
//!   2. Build and run this project:
//!        cargo run

use exportingassemblyne::platform::{self, FailureType};
use exportingassemblyne::exports;

fn on_failure(failure_type: FailureType, error_code: i32) {
    eprintln!(
        "FAILURE: Type: {:?}, Error code: {:#010x}",
        failure_type, error_code
    );
}

fn main() {
    // Set failure callback.
    platform::set_failure_callback(Some(on_failure));

    // Preload the .NET runtime.
    unsafe {
        let result = platform::try_preload_runtime();
        assert!(result.is_ok(), "try_preload_runtime failed: {:#010x}", result.unwrap_err());
        println!("Runtime loaded successfully.");
    }

    // Call .NET exports.
    unsafe {
        let a: i32 = 3;
        let b: i32 = 5;

        let c = exports::IntIntInt(a, b);
        println!("IntIntInt({}, {}) = {}", a, b, c);

        let c = exports::UnmanagedIntIntInt(a, b);
        println!("UnmanagedIntIntInt({}, {}) = {}", a, b, c);
    }
}
