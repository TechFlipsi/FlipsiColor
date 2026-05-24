#[=======================================================================[.rst:
FindOpenCV
----------

Find the OpenCV library.

Imported Targets
^^^^^^^^^^^^^^^

``OpenCV::Core``
``OpenCV::ImgProc``
``OpenCV::ImgCodecs``
``OpenCV::Photo``
``OpenCV::Video``

Result Variables
^^^^^^^^^^^^^^^^

``OpenCV_FOUND``
  True if OpenCV was found.

Hints
^^^^^

``OpenCV_ROOT``
  Root directory of an OpenCV installation (Env: ``OpenCV_ROOT``).

#]=======================================================================]

include(FindPackageHandleStandardArgs)
include(CMakeParseArguments)

# Module list we need
set(_OpenCV_MODULES core imgproc imgcodecs photo video)

# 1. Env-Var (Windows CI pre-built binary)
if(NOT OpenCV_FOUND AND DEFINED ENV{OpenCV_ROOT})
    set(_OpenCV_ROOT "$ENV{OpenCV_ROOT}")
    message(STATUS "FindOpenCV: Using OpenCV_ROOT from env: ${_OpenCV_ROOT}")

    # Find include directory
    find_path(OpenCV_INCLUDE_DIR
        NAMES opencv2/opencv.hpp
        PATHS "${_OpenCV_ROOT}/include" "${_OpenCV_ROOT}/../include"
        NO_DEFAULT_PATH
    )

    if(NOT OpenCV_INCLUDE_DIR)
        # Official Windows exe has includes under build/include
        find_path(OpenCV_INCLUDE_DIR
            NAMES opencv2/opencv.hpp
            PATHS "${_OpenCV_ROOT}"
            NO_DEFAULT_PATH
        )
    endif()

    # Find libraries — search common OpenCV Windows layout paths
    set(_OpenCV_FOUND_ALL TRUE)
    set(_OpenCV_LIBS "")
    foreach(_mod ${_OpenCV_MODULES})
        find_library(OpenCV_${_mod}_IMPLIB
            NAMES "opencv_${_mod}4100" "opencv_${_mod}" "opencv_${_mod}4130"
            PATHS
                "${_OpenCV_ROOT}/x64/vc17/lib"
                "${_OpenCV_ROOT}/x64/vc16/lib"
                "${_OpenCV_ROOT}/lib"
            NO_DEFAULT_PATH
        )
        if(OpenCV_${_mod}_IMPLIB)
            list(APPEND _OpenCV_LIBS "${OpenCV_${_mod}_IMPLIB}")
        else()
            message(STATUS "FindOpenCV: Module ${_mod} not found, trying opencv_world fallback")
            set(_OpenCV_FOUND_ALL FALSE)
        endif()
    endforeach()

    # Fallback: try opencv_world (single-library build)
    if(NOT _OpenCV_FOUND_ALL)
        find_library(OpenCV_WORLD_IMPLIB
            NAMES "opencv_world4100" "opencv_world" "opencv_world4130"
            PATHS
                "${_OpenCV_ROOT}/x64/vc17/lib"
                "${_OpenCV_ROOT}/x64/vc16/lib"
                "${_OpenCV_ROOT}/lib"
            NO_DEFAULT_PATH
        )
        if(OpenCV_WORLD_IMPLIB)
            set(_OpenCV_FOUND_ALL TRUE)
            set(_OpenCV_LIBS "${OpenCV_WORLD_IMPLIB}")
            message(STATUS "FindOpenCV: Using opencv_world fallback")
        endif()
    endif()

    if(OpenCV_INCLUDE_DIR AND _OpenCV_FOUND_ALL)
        set(OpenCV_FOUND TRUE)
        set(OpenCV_INCLUDE_DIRS "${OpenCV_INCLUDE_DIR}")
        set(OpenCV_LIBRARIES "${_OpenCV_LIBS}")

        # Create IMPORTED INTERFACE targets for each module
        foreach(_mod ${_OpenCV_MODULES})
            if(OpenCV_${_mod}_IMPLIB)
                # Find corresponding DLL for runtime
                find_file(OpenCV_${_mod}_DLL
                    NAMES "opencv_${_mod}4100.dll" "opencv_${_mod}.dll"
                    PATHS
                        "${_OpenCV_ROOT}/x64/vc17/bin"
                        "${_OpenCV_ROOT}/x64/vc16/bin"
                        "${_OpenCV_ROOT}/bin"
                    NO_DEFAULT_PATH
                )

                add_library(OpenCV::${_mod} SHARED IMPORTED)
                set_target_properties(OpenCV::${_mod} PROPERTIES
                    IMPORTED_IMPLIB "${OpenCV_${_mod}_IMPLIB}"
                    INTERFACE_INCLUDE_DIRECTORIES "${OpenCV_INCLUDE_DIR}"
                )
                if(OpenCV_${_mod}_DLL)
                    set_target_properties(OpenCV::${_mod} PROPERTIES
                        IMPORTED_LOCATION "${OpenCV_${_mod}_DLL}"
                    )
                endif()
            endif()
        endforeach()

        # If using opencv_world, create individual targets pointing to world
        if(OpenCV_WORLD_IMPLIB)
            find_file(OpenCV_WORLD_DLL
                NAMES "opencv_world4100.dll" "opencv_world.dll"
                PATHS
                    "${_OpenCV_ROOT}/x64/vc17/bin"
                    "${_OpenCV_ROOT}/x64/vc16/bin"
                    "${_OpenCV_ROOT}/bin"
                NO_DEFAULT_PATH
            )
            foreach(_mod ${_OpenCV_MODULES})
                if(NOT TARGET OpenCV::${_mod})
                    add_library(OpenCV::${_mod} SHARED IMPORTED)
                    set_target_properties(OpenCV::${_mod} PROPERTIES
                        IMPORTED_IMPLIB "${OpenCV_WORLD_IMPLIB}"
                        INTERFACE_INCLUDE_DIRECTORIES "${OpenCV_INCLUDE_DIR}"
                    )
                    if(OpenCV_WORLD_DLL)
                        set_target_properties(OpenCV::${_mod} PROPERTIES
                            IMPORTED_LOCATION "${OpenCV_WORLD_DLL}"
                        )
                    endif()
                endif()
            endforeach()
        endif()

        message(STATUS "FindOpenCV: Found via env OpenCV_ROOT: ${_OpenCV_ROOT}")
    else()
        message(STATUS "FindOpenCV: Could not find all modules in ${_OpenCV_ROOT}")
    endif()
endif()

# 2. pkg-config (Linux/macOS)
if(NOT OpenCV_FOUND)
    find_package(PkgConfig)
    if(PKG_CONFIG_FOUND)
        pkg_check_modules(OpenCV QUIET IMPORTED_TARGET opencv4)
        if(OpenCV_FOUND)
            # Create imported targets aliasing PkgConfig targets
            set(_OpenCV_PC_MODULES opencv_core opencv_imgproc opencv_imgcodecs opencv_photo opencv_video)
            foreach(_pc_mod ${_OpenCV_PC_MODULES})
                string(REPLACE "opencv_" "" _mod_name "${_pc_mod}")
                # Capitalize first letter for target naming: core → Core, imgproc → Imgproc
                string(SUBSTRING "${_mod_name}" 0 1 _first)
                string(TOUPPER "${_first}" _first_upper)
                string(LENGTH "${_mod_name}" _len)
                if(_len GREATER 1)
                    math(EXPR _rest_start 1)
                    string(SUBSTRING "${_mod_name}" ${_rest_start} -1 _rest)
                    string(TOLOWER "${_rest}" _rest_lower)
                    set(_target_name "${_first_upper}${_rest_lower}")
                else()
                    set(_target_name "${_first_upper}")
                endif()
                if(NOT TARGET OpenCV::${_target_name})
                    add_library(OpenCV::${_target_name} ALIAS PkgConfig::${_pc_mod})
                endif()
            endforeach()
            message(STATUS "FindOpenCV: Found via pkg-config")
        endif()
    endif()
endif()

# 3. CMake Config fallback (system-installed OpenCV with OpenCVConfig.cmake)
if(NOT OpenCV_FOUND)
    find_path(OpenCV_CONFIG_DIR
        NAMES OpenCVConfig.cmake
        PATHS
            /usr/lib/cmake/opencv4
            /usr/lib64/cmake/opencv4
            /usr/local/lib/cmake/opencv4
    )
    if(OpenCV_CONFIG_DIR)
        include("${OpenCV_CONFIG_DIR}/OpenCVConfig.cmake")
        if(OpenCV_FOUND)
            # OpenCVConfig creates its own targets (opencv_core, etc.)
            # Create aliases for our naming convention
            foreach(_mod ${_OpenCV_MODULES})
                if(TARGET opencv_${_mod} AND NOT TARGET OpenCV::${_mod})
                    add_library(OpenCV::${_mod} ALIAS opencv_${_mod})
                endif()
            endforeach()
            message(STATUS "FindOpenCV: Found via OpenCVConfig.cmake")
        endif()
    endif()
endif()

find_package_handle_standard_args(OpenCV DEFAULT_MSG OpenCV_FOUND)