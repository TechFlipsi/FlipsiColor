# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindLensfun
# ------------
#
# Find the Lensfun library (lens correction database)
#
# Fallback module for systems without CMake config (Ubuntu 24.04).
#
# Imported Targets:
#   Lensfun::Lensfun
#
# Result Variables:
#   Lensfun_FOUND
#   Lensfun_INCLUDE_DIRS
#   Lensfun_LIBRARIES
#   Lensfun_VERSION

include(FindPackageHandleStandardArgs)

find_package(PkgConfig QUIET)
if(PKG_CONFIG_FOUND)
  pkg_check_modules(PC_LENSFUN QUIET lensfun)
endif()

find_path(Lensfun_INCLUDE_DIR
  NAMES lensfun.h
  HINTS
    ${PC_LENSFUN_INCLUDE_DIRS}
    /usr/include
    /usr/local/include
  PATH_SUFFIXES include/lensfun
)

find_library(Lensfun_LIBRARY
  NAMES lensfun
  HINTS
    ${PC_LENSFUN_LIBRARY_DIRS}
    /usr/lib
    /usr/local/lib
    /usr/lib/x86_64-linux-gnu
  PATH_SUFFIXES lib
)

if(PC_LENSFUN_VERSION)
  set(Lensfun_VERSION ${PC_LENSFUN_VERSION})
endif()

find_package_handle_standard_args(Lensfun
  REQUIRED_VARS Lensfun_LIBRARY Lensfun_INCLUDE_DIR
  VERSION_VAR Lensfun_VERSION
)

if(Lensfun_FOUND AND NOT TARGET Lensfun::Lensfun)
  add_library(Lensfun::Lensfun UNKNOWN IMPORTED)
  set_target_properties(Lensfun::Lensfun PROPERTIES
    IMPORTED_LOCATION "${Lensfun_LIBRARY}"
    INTERFACE_INCLUDE_DIRECTORIES "${Lensfun_INCLUDE_DIR}"
  )
endif()

mark_as_advanced(Lensfun_INCLUDE_DIR Lensfun_LIBRARY Lensfun_VERSION)