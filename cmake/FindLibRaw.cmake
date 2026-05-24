# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindLibRaw
# -----------
#
# Find the LibRaw library (RAW image decoder)
#
# This is a fallback module for systems where libraw-dev does not ship
# a CMake config file (e.g., Ubuntu 24.04 apt installs only pkg-config).
#
# Imported Targets:
#   LibRaw::LibRaw
#
# Result Variables:
#   LibRaw_FOUND       - True if LibRaw was found
#   LibRaw_INCLUDE_DIRS - Include directories
#   LibRaw_LIBRARIES    - Libraries to link
#   LibRaw_VERSION      - Version string

include(FindPackageHandleStandardArgs)

# Try pkg-config first (covers most Linux distros)
find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(PC_LIBRAW QUIET libraw)
endif()

# Find header
find_path(LibRaw_INCLUDE_DIR
  NAMES libraw/libraw.h
  HINTS
    ${PC_LIBRAW_INCLUDE_DIRS}
    /usr/include
    /usr/local/include
  PATH_SUFFIXES include
)

# Find library
find_library(LibRaw_LIBRARY
  NAMES raw
  HINTS
    ${PC_LIBRAW_LIBRARY_DIRS}
    /usr/lib
    /usr/local/lib
    /usr/lib/x86_64-linux-gnu
  PATH_SUFFIXES lib
)

# Version from pkg-config
if(PC_LIBRAW_VERSION)
  set(LibRaw_VERSION ${PC_LIBRAW_VERSION})
elseif(LibRaw_INCLUDE_DIR AND EXISTS "${LibRaw_INCLUDE_DIR}/libraw/libraw_version.h")
  file(STRINGS "${LibRaw_INCLUDE_DIR}/libraw/libraw_version.h" _libraw_version_str
    REGEX "^#define[ \t]+LIBRAW_VERSION[^_]")
  string(REGEX REPLACE "^.*LIBRAW_VERSION_STR[ \t]+\"([^\"]+)\".*$" "\\1"
    LibRaw_VERSION "${_libraw_version_str}")
endif()

find_package_handle_standard_args(LibRaw
  REQUIRED_VARS LibRaw_LIBRARY LibRaw_INCLUDE_DIR
  VERSION_VAR LibRaw_VERSION
)

if(LibRaw_FOUND AND NOT TARGET LibRaw::LibRaw)
  add_library(LibRaw::LibRaw UNKNOWN IMPORTED)
  set_target_properties(LibRaw::LibRaw PROPERTIES
    IMPORTED_LOCATION "${LibRaw_LIBRARY}"
    INTERFACE_INCLUDE_DIRECTORIES "${LibRaw_INCLUDE_DIR}"
  )
endif()

mark_as_advanced(LibRaw_INCLUDE_DIR LibRaw_LIBRARY LibRaw_VERSION)