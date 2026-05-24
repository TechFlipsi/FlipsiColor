# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# Findonnxruntime
# ---------------
#
# Find ONNX Runtime library
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

# Try pkg-config first
find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(PC_onnxruntime QUIET onnxruntime)
endif()

# Find header
find_path(onnxruntime_INCLUDE_DIR
  NAMES core/session/onnxruntime_cxx_api.h
  HINTS
    ${PC_onnxruntime_INCLUDE_DIRS}
    /usr/local/include
    /usr/include
    /opt/homebrew/include
  PATH_SUFFIXES onnxruntime include/onnxruntime
)

# Find library
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

# Version
if(PC_onnxruntime_VERSION)
  set(onnxruntime_VERSION ${PC_onnxruntime_VERSION})
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