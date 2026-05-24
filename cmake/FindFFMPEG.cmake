# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindFFMPEG
# ----------
#
# Find FFmpeg libraries (avcodec, avformat, avutil, swscale, swresample)
#
# Fallback module for systems without CMake config (Ubuntu 24.04 apt).
#
# Imported Targets:
#   FFMPEG::avcodec, FFMPEG::avformat, FFMPEG::avutil,
#   FFMPEG::swscale, FFMPEG::swresample
#
# Result Variables:
#   FFMPEG_FOUND
#   FFMPEG_INCLUDE_DIRS
#   FFMPEG_LIBRARIES
#   FFMPEG_VERSION

include(FindPackageHandleStandardArgs)

find_package(PkgConfig QUIET)

# ── Required components ───────────────────────────────────────────────────
set(_FFMPEG_REQUIRED_COMPONENTS avcodec avformat avutil swscale swresample)
set(_FFMPEG_LIBRARIES)
set(_FFMPEG_INCLUDE_DIRS)
set(_FFMPEG_ALL_FOUND TRUE)

foreach(_comp ${_FFMPEG_REQUIRED_COMPONENTS})
  # Try pkg-config first
  if(PKG_CONFIG_FOUND)
    pkg_check_modules(PC_FFMPEG_${_comp} QUIET lib${_comp})
  endif()

  find_path(FFMPEG_${_comp}_INCLUDE_DIR
    NAMES lib${_comp}/version.h
    HINTS
      ${PC_FFMPEG_${_comp}_INCLUDE_DIRS}
      /usr/include
      /usr/local/include
      /opt/homebrew/include
    PATH_SUFFIXES include ffmpeg
  )

  # Fallback: ffmpeg headers are in a shared directory
  if(NOT FFMPEG_${_comp}_INCLUDE_DIR)
    find_path(FFMPEG_${_comp}_INCLUDE_DIR
      NAMES lib${_comp}/version.h
      HINTS
        /usr/include/x86_64-linux-gnu
    )
  endif()

  find_library(FFMPEG_${_comp}_LIBRARY
    NAMES ${_comp}
    HINTS
      ${PC_FFMPEG_${_comp}_LIBRARY_DIRS}
      /usr/lib
      /usr/local/lib
      /usr/lib/x86_64-linux-gnu
      /opt/homebrew/lib
    PATH_SUFFIXES lib
  )

  if(FFMPEG_${_comp}_LIBRARY AND FFMPEG_${_comp}_INCLUDE_DIR)
    list(APPEND _FFMPEG_LIBRARIES ${FFMPEG_${_comp}_LIBRARY})
    list(APPEND _FFMPEG_INCLUDE_DIRS ${FFMPEG_${_comp}_INCLUDE_DIR})
    if(NOT TARGET FFMPEG::${_comp})
      add_library(FFMPEG::${_comp} UNKNOWN IMPORTED)
      set_target_properties(FFMPEG::${_comp} PROPERTIES
        IMPORTED_LOCATION "${FFMPEG_${_comp}_LIBRARY}"
        INTERFACE_INCLUDE_DIRECTORIES "${FFMPEG_${_comp}_INCLUDE_DIR}"
      )
    endif()
  else()
    set(_FFMPEG_ALL_FOUND FALSE)
    message(STATUS "FFmpeg component ${_comp} NOT found")
  endif()
endforeach()

# Deduplicate include dirs
if(_FFMPEG_INCLUDE_DIRS)
  list(REMOVE_DUPLICATES _FFMPEG_INCLUDE_DIRS)
endif()

set(FFMPEG_INCLUDE_DIRS ${_FFMPEG_INCLUDE_DIRS})
set(FFMPEG_LIBRARIES ${_FFMPEG_LIBRARIES})

# Version from pkg-config (avcodec version = ffmpeg version)
if(PC_FFMPEG_avcodec_VERSION)
  set(FFMPEG_VERSION ${PC_FFMPEG_avcodec_VERSION})
endif()

find_package_handle_standard_args(FFMPEG
  REQUIRED_VARS _FFMPEG_ALL_FOUND FFMPEG_LIBRARIES FFMPEG_INCLUDE_DIRS
  VERSION_VAR FFMPEG_VERSION
  HANDLE_COMPONENTS
)