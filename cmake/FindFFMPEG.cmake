# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindFFMPEG
# ----------
#
# Find FFmpeg libraries (avcodec, avformat, avutil, swscale, swresample)
#
# Uses pkg-config for discovery. Falls back to manual search.
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

find_package(PkgConfig REQUIRED)

set(_FFMPEG_COMPONENTS avcodec avformat avutil swscale swresample)
set(_FFMPEG_FOUND_ALL TRUE)
set(FFMPEG_INCLUDE_DIRS)
set(FFMPEG_LIBRARIES)

foreach(_comp ${_FFMPEG_COMPONENTS})
  # pkg-config discovery (libavcodec, libavformat etc.)
  pkg_check_modules(PC_FFMPEG_${_comp} QUIET IMPORTED_TARGET lib${_comp})

  if(TARGET PkgConfig::PC_FFMPEG_${_comp})
    # Create alias target with canonical name
    if(NOT TARGET FFMPEG::${_comp})
      add_library(FFMPEG::${_comp} ALIAS PkgConfig::PC_FFMPEG_${_comp})
    endif()
    list(APPEND FFMPEG_LIBRARIES ${PC_FFMPEG_${_comp}_LIBRARIES})
    list(APPEND FFMPEG_INCLUDE_DIRS ${PC_FFMPEG_${_comp}_INCLUDE_DIRS})
    message(STATUS "Found FFmpeg ${_comp}: ${PC_FFMPEG_${_comp}_VERSION}")
  else()
    # Fallback: manual search
    find_library(FFMPEG_${_comp}_LIBRARY
      NAMES ${_comp}
      HINTS /usr/lib /usr/local/lib /usr/lib/x86_64-linux-gnu /opt/homebrew/lib
      PATH_SUFFIXES lib
    )
    find_path(FFMPEG_${_comp}_INCLUDE_DIR
      NAMES version.h
      HINTS /usr/include /usr/local/include /opt/homebrew/include
      PATH_SUFFIXES lib${_comp}
    )

    if(FFMPEG_${_comp}_LIBRARY AND FFMPEG_${_comp}_INCLUDE_DIR)
      if(NOT TARGET FFMPEG::${_comp})
        add_library(FFMPEG::${_comp} UNKNOWN IMPORTED)
        set_target_properties(FFMPEG::${_comp} PROPERTIES
          IMPORTED_LOCATION "${FFMPEG_${_comp}_LIBRARY}"
          INTERFACE_INCLUDE_DIRECTORIES "${FFMPEG_${_comp}_INCLUDE_DIR}"
        )
      endif()
      list(APPEND FFMPEG_LIBRARIES ${FFMPEG_${_comp}_LIBRARY})
      list(APPEND FFMPEG_INCLUDE_DIRS ${FFMPEG_${_comp}_INCLUDE_DIR})
      message(STATUS "Found FFmpeg ${_comp}: ${FFMPEG_${_comp}_LIBRARY}")
    else()
      set(_FFMPEG_FOUND_ALL FALSE)
      message(STATUS "FFmpeg ${_comp} NOT found")
    endif()
  endif()
endforeach()

# Deduplicate include dirs
if(FFMPEG_INCLUDE_DIRS)
  list(REMOVE_DUPLICATES FFMPEG_INCLUDE_DIRS)
endif()

# Version from pkg-config (avcodec version)
pkg_check_modules(PC_FFMPEG_avcodec_ver QUIET libavcodec)
if(PC_FFMPEG_avcodec_ver_VERSION)
  set(FFMPEG_VERSION ${PC_FFMPEG_avcodec_ver_VERSION})
endif()

find_package_handle_standard_args(FFMPEG
  REQUIRED_VARS _FFMPEG_FOUND_ALL FFMPEG_LIBRARIES FFMPEG_INCLUDE_DIRS
  VERSION_VAR FFMPEG_VERSION
  HANDLE_COMPONENTS
)