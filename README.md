IGE
===

IGE (Intellectual Game Engine) - a LGPL licensed .NET class library that can
be used to create games and can be of some use in creating applications.

Sources and modules
===================

IGE consists of multiple modules where each module is a separate project added
to this repository as a submodule. You can clone the IGE repository along with
all submodules by using following command:

	git clone --recursive https://github.com/destrofer/IGE

In case you already cloned this repository and module projects don't contain
any sources you may use following commands to get all the sources:

	git submodule init
	git submodule update

Every time you pull updates from IGE repository you must use the following
command to update all submodules as well:

	git submodule update
	
We recommend reading more about git submodules in case you don't know how to
handle them.

Versioning policy
=================

Version of each module as well as the IGE itself consists of 3 components:
<major>.<minor>.<revision>

When changing versions or checking versions for compatibility follow these
simple rules:

Major version is increased only when there are very big changes like when code
is being fully rewritten or namespaces/classes migrate from module to module.
When major version is increased the minor and revision are reset to 0.

Minor version is increased when incompatible changes are made: class was
renamed or moved to another namespace, method was renamed or it's signature was
changed, field was renamed, changed it's type of accessibility, etc. It will
also be increased when new fields are added to existing structures or classes.
When minor version changes revision is reset to 0.

Revision version is increased only when compatible changes such as bug fixes or
algorithm improvements are made. It also is increased when new classes, class
methods or properties are added.

Compiled module library files will also have fourth version component <build>,
but it may be ignored as it is generated automatically depending on time of
compilation and the next release may have build version smaller than the
previous one.

Examples of comparing versions (consider version 2.3.5 is required):

- v2.3.5 is compatible as it is exactly what we require.
- v2.3.6 is compatible since only compatible changes are made.
- v2.3.4 is not compatible since it might not have required classes/methods.
- v2.4.5 is not compatible since required classes/methods may have changed.
- v2.2.5 is not compatible since it might not have required classes/methods.
- v3.3.5 is not compatible since required classes/methods may have changed.
- v1.3.5 is not compatible since it might not have required classes/methods.

Licensing
=========

Copyright 2004-2015 Viacheslav Soroka

IGE is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

IGE is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with IGE. If not, see <http://www.gnu.org/licenses/>.

You may find original IGE repository at https://github.com/destrofer/IGE in
case you have received a copy and don't know where it came from.
