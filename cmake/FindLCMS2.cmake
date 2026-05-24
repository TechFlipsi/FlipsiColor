# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindLCMS2
# ---------
#
# Find the Little CMS library (color management)
#
# Fallback module for systems where lcms2 ships as "liblcms2" or
# "little-cms2" (Homebrew) but has no CMake config file.
#
# Imported Targets:
#   LCMS2::LCMS2
#
# Result Variables:
#   LCMS2_FOUND
#   LCMS2_INCLUDE_DIRS
#   LCMS2_LIBRARIES
#   LCMS2_VERSION

include(FindPackageHandleStandardArgs)

find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(PC_LCMS2 QUIET lcms2)
endif()

# Find header
find_path(LCMS2_INCLUDE_DIR
  NAMES lcms2.h
  HINTS
    ${PC_LCMS2_INCLUDE_DIRS}
    /usr/include
    /usr/local/include
    /opt/homebrew/include
    /opt/homebrew/opt/little-cms2/include
  PATH_SUFFIXES include
)

# Find library
find_library(LCMS2_LIBRARY
  NAMES lcms2 lcms2_static
  HINTS
    ${PC_LCMS2_LIBRARY_DIRS}
    /usr/lib
    /usr/local/lib
    /usr/lib/x86_64-linux-gnu
    /opt/homebrew/lib
    /opt/homebrew/opt/little-cms2/lib
  PATH_SUFFIXES lib
)

# Version
if(PC_LCMS2_VERSION)
  set(LCMS2_VERSION ${PC_LCMS2_VERSION})
elseif(LCMS2_INCLUDE_DIR AND EXISTS "${LCMS2_INCLUDE_DIR}/lcms2.h")
  file(STRINGS "${LCMS2_INCLUDE_DIR}/lcms2.h" _lcms2_version_str
    REGEX "^#define[ \t]+LCMS_VERSION[ \t]+[0-9]+")
  string(REGEX REPLACE "^.*LCMS_VERSION[ \t]+([0-9]+).*$" "\\1"
    _lcms2_version_int "${_lcms2_version_str}")
  # LCMS_VERSION is 2070 for 2.7.0, 2080 for 2.8.0, etc.
  math(EXPR LCMS2_MAJOR "${_lcms2_version_int} / 1000")
  math(EXPR LCMS2_MINOR "(${_lcms2_version_int} % 1000) / 10")
  math(EXPR LCMS2_PATCH "${_lcms2_version_int} % 10")
  set(LCMS2_VERSION "${LCMS2_MAJOR}.${LCMS2_MINOR}.${LCMS2_PATCH}")
endif()

find_package_handle_standard_args(LCMS2
  REQUIRED_VARS LCMS2_LIBRARY LCMS2_INCLUDE_DIR
  VERSION_VAR LCMS2_VERSION
)

if(LCMS2_FOUND AND NOT TARGET LCMS2::LCMS2)
  add_library(LCMS2::LCMS2 UNKNOWN IMPORTED)
  set_target_properties(LCMS2::LCMS2 PROPERTIES
    IMPORTED_LOCATION "${LCMS2_LIBRARY}"
    INTERFACE_INCLUDE_DIRECTORIES "${LCMS2_INCLUDE_DIR}"
  )
endif()

mark_as_advanced(LCMS2_INCLUDE_DIR LCMS2_LIBRARY LCMS2_VERSION)