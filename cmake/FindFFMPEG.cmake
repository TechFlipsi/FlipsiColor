# Distributed under the OSI-approved BSD 3-Clause License.
# Copyright 2026 Fabian Kirchweger (TechFlipsi)
# SPDX-License-Identifier: BSD-3-Clause
#
# FindFFMPEG
# ----------
#
# Find FFmpeg libraries (avcodec, avformat, avutil, swscale, swresample)
#
# Discovery order:
#   1. Environment variables (FFMPEG_DIR, FFMPEG_INCLUDE_DIRS, FFMPEG_LIBRARIES)
#   2. pkg-config (Linux/macOS)
#   3. Manual library search fallback
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

set(_FFMPEG_COMPONENTS avcodec avformat avutil swscale swresample)
set(_FFMPEG_FOUND_ALL TRUE)
set(FFMPEG_INCLUDE_DIRS)
set(FFMPEG_LIBRARIES)

# ── Strategy 1: Environment variables (Windows pre-built) ──────
# FFMPEG_DIR, FFMPEG_INCLUDE_DIRS, FFMPEG_LIBRARY_DIR or FFMPEG_LIBRARIES
if(FFMPEG_DIR OR FFMPEG_INCLUDE_DIRS)
  message(STATUS "FindFFMPEG: Using environment variable paths")

  if(NOT FFMPEG_INCLUDE_DIRS)
    set(FFMPEG_INCLUDE_DIRS "${FFMPEG_DIR}/include")
  endif()

  # Split semicolon-separated include dirs
  string(REPLACE ";" ";" FFMPEG_INCLUDE_DIRS_LIST "${FFMPEG_INCLUDE_DIRS}")

  set(_FFMPEG_INC_DIR "")
  foreach(_inc_dir ${FFMPEG_INCLUDE_DIRS_LIST})
    if(EXISTS "${_inc_dir}")
      set(_FFMPEG_INC_DIR "${_inc_dir}")
      break()
    endif()
  endforeach()
  if(NOT _FFMPEG_INC_DIR AND EXISTS "${FFMPEG_DIR}/include")
    set(_FFMPEG_INC_DIR "${FFMPEG_DIR}/include")
  endif()

  foreach(_comp ${_FFMPEG_COMPONENTS})
    # Find library
    if(FFMPEG_LIBRARY_DIR)
      find_library(FFMPEG_${_comp}_LIBRARY
        NAMES ${_comp} lib${_comp}
        HINTS "${FFMPEG_LIBRARY_DIR}"
        PATH_SUFFIXES lib
      )
    elseif(FFMPEG_DIR)
      find_library(FFMPEG_${_comp}_LIBRARY
        NAMES ${_comp} lib${_comp}
        HINTS "${FFMPEG_DIR}"
        PATH_SUFFIXES lib bin
      )
    endif()

    # Find include dir
    find_path(FFMPEG_${_comp}_INCLUDE_DIR
      NAMES version.h
      HINTS ${_FFMPEG_INC_DIR}
      PATH_SUFFIXES lib${_comp} ${_comp}
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
      message(STATUS "Found FFmpeg ${_comp}: ${FFMPEG_${_comp}_LIBRARY} (env)")
    else()
      set(_FFMPEG_FOUND_ALL FALSE)
      message(WARNING "FFmpeg ${_comp} NOT found via environment variables")
    endif()
  endforeach()

  set(FFMPEG_VERSION "7.1")

# ── Strategy 2: pkg-config (Linux/macOS) ──────
elseif(PkgConfig_FOUND OR (NOT WIN32))
  find_package(PkgConfig QUIET)

  foreach(_comp ${_FFMPEG_COMPONENTS})
    pkg_check_modules(PC_${_comp} QUIET IMPORTED_TARGET lib${_comp})

    if(PC_${_comp}_FOUND)
      if(NOT TARGET FFMPEG::${_comp})
        add_library(FFMPEG::${_comp} ALIAS PkgConfig::PC_${_comp})
      endif()
      list(APPEND FFMPEG_LIBRARIES ${PC_${_comp}_LIBRARIES})
      list(APPEND FFMPEG_INCLUDE_DIRS ${PC_${_comp}_INCLUDE_DIRS})
      message(STATUS "Found FFmpeg ${_comp}: ${PC_${_comp}_VERSION} (pkg-config)")
    else()
      # Fallback: manual search by library name
      find_library(FFMPEG_${_comp}_LIBRARY
        NAMES ${_comp} lib${_comp}
        HINTS /usr/lib /usr/local/lib /usr/lib/x86_64-linux-gnu
              /opt/homebrew/lib /usr/lib/aarch64-linux-gnu
        PATH_SUFFIXES lib
      )
      find_path(FFMPEG_${_comp}_INCLUDE_DIR
        NAMES version.h
        HINTS /usr/include /usr/local/include /opt/homebrew/include
        PATH_SUFFIXES lib${_comp} ${_comp}
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
        message(STATUS "Found FFmpeg ${_comp}: ${FFMPEG_${_comp}_LIBRARY} (manual)")
      else()
        set(_FFMPEG_FOUND_ALL FALSE)
        message(STATUS "FFmpeg ${_comp} NOT found")
      endif()
    endif()
  endforeach()

  # Version from pkg-config
  if(PC_avcodec_VERSION)
    set(FFMPEG_VERSION ${PC_avcodec_VERSION})
  endif()

else()
  set(_FFMPEG_FOUND_ALL FALSE)
  message(STATUS "FindFFMPEG: No discovery method available")
endif()

# Deduplicate include dirs
if(FFMPEG_INCLUDE_DIRS)
  list(REMOVE_DUPLICATES FFMPEG_INCLUDE_DIRS)
endif()

find_package_handle_standard_args(FFMPEG
  REQUIRED_VARS _FFMPEG_FOUND_ALL
  VERSION_VAR FFMPEG_VERSION
)