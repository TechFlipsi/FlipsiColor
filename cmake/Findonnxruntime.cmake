# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# Findonnxruntime
# ---------------
#
# Find ONNX Runtime library
#
# Discovery order:
#   1. Environment variable ONNXRUNTIME_DIR (Windows pre-built)
#   2. pkg-config (Linux/macOS)
#   3. Manual library search fallback
#
# Imported Targets:
#   onnxruntime
#
# Result Variables:
#   onnxruntime_FOUND
#   onnxruntime_INCLUDE_DIRS
#   onnxruntime_LIBRARIES
#   onnxruntime_VERSION

include(FindPackageHandleStandardArgs)

# ── Strategy 1: Environment variable ──────
if(ONNXRUNTIME_DIR AND EXISTS "${ONNXRUNTIME_DIR}")
  message(STATUS "Findonnxruntime: Using ONNXRUNTIME_DIR = ${ONNXRUNTIME_DIR}")

  find_path(onnxruntime_INCLUDE_DIR
    NAMES onnxruntime_cxx_api.h
    HINTS "${ONNXRUNTIME_DIR}/include"
    PATH_SUFFIXES onnxruntime include/onnxruntime
  )

  find_library(onnxruntime_LIBRARY
    NAMES onnxruntime libonnxruntime
    HINTS "${ONNXRUNTIME_DIR}/lib" "${ONNXRUNTIME_DIR}"
    PATH_SUFFIXES lib lib64
  )

  set(onnxruntime_VERSION "1.20.0")

# ── Strategy 2: pkg-config (Linux/macOS) ──────
else()
  find_package(PkgConfig QUIET)
  if(PKG_CONFIG_FOUND)
    pkg_check_modules(PC_onnxruntime QUIET onnxruntime)
  endif()

  find_path(onnxruntime_INCLUDE_DIR
    NAMES onnxruntime_cxx_api.h
    HINTS
      ${PC_onnxruntime_INCLUDE_DIRS}
      /usr/local/include
      /usr/include
      /opt/homebrew/include
      /opt/homebrew/include/onnxruntime
    PATH_SUFFIXES onnxruntime include/onnxruntime
  )

  find_library(onnxruntime_LIBRARY
    NAMES onnxruntime libonnxruntime
    HINTS
      ${PC_onnxruntime_LIBRARY_DIRS}
      /usr/local/lib
      /usr/local/lib64
      /usr/lib
      /usr/lib/x86_64-linux-gnu
      /opt/homebrew/lib
    PATH_SUFFIXES lib lib64
  )

  if(PC_onnxruntime_VERSION)
    set(onnxruntime_VERSION ${PC_onnxruntime_VERSION})
  endif()
endif()

find_package_handle_standard_args(onnxruntime
  REQUIRED_VARS onnxruntime_LIBRARY onnxruntime_INCLUDE_DIR
  VERSION_VAR onnxruntime_VERSION
)

if(onnxruntime_FOUND AND NOT TARGET onnxruntime)
  add_library(onnxruntime SHARED IMPORTED)
  set_target_properties(onnxruntime PROPERTIES
    IMPORTED_LOCATION "${onnxruntime_LIBRARY}"
    INTERFACE_INCLUDE_DIRECTORIES "${onnxruntime_INCLUDE_DIR}"
  )
endif()

mark_as_advanced(onnxruntime_INCLUDE_DIR onnxruntime_LIBRARY onnxruntime_VERSION)