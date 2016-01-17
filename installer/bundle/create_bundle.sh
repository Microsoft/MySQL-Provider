#!/bin/bash
#
#
# This script will create a bundle file given an existing kit.
#
# Parameters:
#	$1: Platform type
#	$2: Directory to package file
#	$3: Package name for MySQL package (without extension)
#
# We expect this script to run from the BUILD directory (i.e. mysql/build).
# Directory paths are hard-coded for this location.

SOURCE_DIR=`(cd ../installer/bundle; pwd -P)`
INTERMEDIATE_DIR=`(mkdir -p ../installer/intermediate; cd ../installer/intermediate; pwd -P)`

# Exit on error
set -e

# Don't display output
set +x

usage()
{
    echo "usage: $0 platform directory mysql-package-name"
    echo "  where"
    echo "    platform is one of: Linux_REDHAT, Linux_SUSE, Linux_ULINUX"
    echo "    directory is directory path to package file"
    echo "    mysql-package-name is the name of the MySQL installation package"
    exit 1
}

# Validate parameters

if [ -z "$1" ]; then
    echo "Missing parameter: Platform type" >&2
    echo ""
    usage
    exit 1
fi

case "$1" in
    Linux_REDHAT|Linux_SUSE|Linux_ULINUX)
	;;

    *)
	echo "Invalid platform type specified: $1" >&2
	exit 1
esac

if [ -z "$2" ]; then
    echo "Missing parameter: Directory to platform file" >&2
    echo ""
    usage
    exit 1
fi

if [ ! -d "$2" ]; then
    echo "Directory \"$2\" does not exist" >&2
    exit 1
fi

if [ -z "$3" ]; then
    echo "Missing parameter: MySQL-package-name" >&2
    echo ""
    usage
    exit 1
fi

if [ ! -f "$2/$3".tar ]; then
    echo "Tar file \"$2/$3\" does not exist"
    exit 1
fi

# Determine the output file name
OUTPUT_DIR=`(cd $2; pwd -P)`

# Work from the temporary directory from this point forward

cd $INTERMEDIATE_DIR

# Fetch the bundle skeleton file
cp $SOURCE_DIR/primary.skel .
chmod u+w primary.skel

# See if we can resolve git references for output
# (See if we can find the master project)
if [ -f ../../../.gitmodules ]; then
    TEMP_FILE=/tmp/create_bundle.$$

    # Get the git reference hashes in a file
    (
       cd ../../..
       echo "Entering 'superproject'" > $TEMP_FILE
       git rev-parse HEAD >> $TEMP_FILE
       git submodule foreach git rev-parse HEAD >> $TEMP_FILE
    )

    # Change lines like: "Entering 'omi'\n<refhash>" to "omi: <refhash>"
    perl -i -pe "s/Entering '([^\n]*)'\n/\$1: /" $TEMP_FILE

    # Grab the reference hashes in a variable
    SOURCE_REFS=`cat $TEMP_FILE`
    rm $TEMP_FILE

    # Update the bundle file w/the ref hash (much easier with perl since multi-line)
    perl -i -pe "s/-- Source code references --/${SOURCE_REFS}/" primary.skel
else
    echo "Unable to find git superproject!" >& 2
    exit 1
fi

# Edit the bundle file for hard-coded values
sed -e "s/PLATFORM=<PLATFORM_TYPE>/PLATFORM=$1/" < primary.skel > primary.$$
mv primary.$$ primary.skel

sed -e "s/MYSQL_PKG=<MYSQL_PKG>/MYSQL_PKG=$3/" < primary.skel > primary.$$
mv primary.$$ primary.skel

SCRIPT_LEN=`wc -l < primary.skel | sed -e 's/ //g'`
SCRIPT_LEN_PLUS_ONE="$((SCRIPT_LEN + 1))"

sed -e "s/SCRIPT_LEN=<SCRIPT_LEN>/SCRIPT_LEN=${SCRIPT_LEN}/" < primary.skel > primary.$$
mv primary.$$ primary.skel

sed -e "s/SCRIPT_LEN_PLUS_ONE=<SCRIPT_LEN+1>/SCRIPT_LEN_PLUS_ONE=${SCRIPT_LEN_PLUS_ONE}/" < primary.skel > primary.$$
mv primary.$$ primary.skel


# Fetch the kit
cp ${OUTPUT_DIR}/${3}.tar .

# Build the bundle
BUNDLE_FILE=${3}.sh
gzip -c ${3}.tar | cat primary.skel - > $BUNDLE_FILE
chmod +x $BUNDLE_FILE
rm primary.skel

# Remove the kit and copy the bundle to the kit location
rm ${3}.tar
mv $BUNDLE_FILE $OUTPUT_DIR/

exit 0
