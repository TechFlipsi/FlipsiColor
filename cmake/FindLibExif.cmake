# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindLibExif
# ------------
#
# Find the LibExif library (EXIF metadata reader/writer)
#
# Fallback module for systems without CMake config (Ubuntu 24.04).
#
# Imported Targets:
#   LibExif::LibExif
#
# Result Variables:
#   LibExif_FOUND
#   LibExif_INCLUDE_DIRS
#   LibExif_LIBRARIES
#   LibExif_VERSION

include(FindPackageHandleStandardArgs)

find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(PC_LIBEXIF QUIET libexif)
endif()

find_path(LibExif_INCLUDE_DIR
  NAMES exif-data.h
  HINTS
    ${PC_LIBEXIF_INCLUDE_DIRS}
    /usr/include
    /usr/local/include
  PATH_SUFFIXES include/libexif
)

find_library(LibExif_LIBRARY
  NAMES exif
  HINTS
    ${PC_LIBEXIF_LIBRARY_DIRS}
    /usr/lib
    /usr/local/lib
    /usr/lib/x86_64-linux-gnu
  PATH_SUFFIXES lib
)

if(PC_LIBEXIF_VERSION)
  set(LibExif_VERSION ${PC_LIBEXIF_VERSION})
endif()

find_package_handle_standard_args(LibExif
  REQUIRED_VARS LibExif_LIBRARY LibExif_INCLUDE_DIR
  VERSION_VAR LibExif_VERSION
)

if(LibExif_FOUND AND NOT TARGET LibExif::LibExif)
  add_library(LibExif::LibExif UNKNOWN IMPORTED)
  set_target_properties(LibExif::LibExif PROPERTIES
    IMPORTED_LOCATION "${LibExif_LIBRARY}"
    INTERFACE_INCLUDE_DIRECTORIES "${LibExif_INCLUDE_DIR}"
  )
endif()

mark_as_advanced(LibExif_INCLUDE_DIR LibExif_LIBRARY LibExif_VERSION)