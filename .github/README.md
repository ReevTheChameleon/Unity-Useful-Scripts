# Unity Useful Scripts - สคริปดีมีประโยชน์ ^^
by Reev the Chameleon  
v1.19.1  

## What Is It?
Just a collection of Unity scripts that may be useful.
They were codes I wrote when trying to make games in the past
that I considered may also be reusuable for my future games.
Before I knew it, I have collected them together and packaged them
into a package so it can be easily imported into Unity project easily
via Unity Package Manager.

## What Is In It?
The content contains anything ranging from:
- Runtime scripts that I have used several times through many projects,
the ones that will continue to evolve.
- Editor scripts that helps make my life navigating the Unity Editor
itself easier. In particular, the package may add extra menus to
the menu bar and context menus within the Unity Editor.
- Unused scripts which I wrote once, but found that they were either
not useful or had been replaced with something better. In essence,
they are in the process where I am deciding whether to remove them or not.

Note: I have not separated Editor code from runtime code.
Usually, Editor code will goes into Unity's special "Editor" folder,
which will be excluded on build. Many of my scripts, however,
have mixing runtime code and Unity code. Editor code will still be
excluded from build due to #if UNITY_EDITOR preprocessor, though.
Admittedly, this may cause difficulty if one tries to compile them
into .dll, but that was not my intention from the beginning.

## How to Install the Package
1) Unzip the package if not done already
2) In Unity, on the menu bar, go to Window -> Package Manager
3) Click the small + button near the top-left of the Package Manager window
4) Select "Add package from disk..."
5) Browse to the location of this package, then select "package.json"
and click Open.

<img src="https://user-images.githubusercontent.com/105905612/226392396-2d7c2a24-c201-4b54-857f-3c27929d280e.png" width="30%"/> <img src="https://user-images.githubusercontent.com/105905612/226392410-f8148489-efe3-4a58-9fba-147928b0fb61.png" width="30%"/> <img src="https://user-images.githubusercontent.com/105905612/226425392-341d8384-6918-4773-8248-64a493195c97.png" width="38%"/>

Your package should then be automatically added to the project.

## About License
Unless specified in THIRDPARTYNOTICE.md, all scripts are under
**MIT license**, one of the most permissive open source license around.
Essentially, it means you can use the package freely, even commercially,
and even modify the package scripts, as long as you retain LICENSE.md file
(specifically the copyright notice and the permissive notice text)
found within the package. (Admittedly, the package is not very well organized,
and hence you may probably decide to include only part of it in your project.)

However, I would also be happy if the code in this package somehow inspires
or sparks you ideas to write your own code to solve your specific problems.
In that case, since you do not use the code in this package directly,
you technically do not need to give attribution, although a little comment
is very appreciated.

## Buy Me a Coffee
I am not an expert programmers and am still learning how to code even
to these days. Countless problems have appeared during all my past projects,
and some takes months just to resolve to a mere acceptable degree.
When such things occured, I searched high and low for "anything" that can just
hint to the solution.

This package reflects those countless struggling in the past and collects
the knowledge I have gained over that time. Hence, I would be really happy if
the code in this package can somehow help you in any way, as the package here 
itself is the condensation of countless helps I have recieved from others
in the past as well.

If you find the code here useful and want to support me,
you may want to "buy me a coffee" at:
 
[<img src="https://storage.ko-fi.com/cdn/brandasset/kofi_button_stroke.png" width="20%"/>](https://ko-fi.com/reevthechameleon)  
[ko-fi.com/reevthechameleon](https://ko-fi.com/reevthechameleon)

Note: Sometimes I may buy an iced-chocolate instead.


<!-- Reference of how to write GitHub markdown language:
https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax
https://stackoverflow.com/questions/8655937/what-is-the-difference-between-readme-and-readme-md-in-github-projects
https://stackoverflow.com/questions/61071158/add-image-with-link-in-githubs-readme-md
https://stackoverflow.com/questions/24319505/how-can-one-display-images-side-by-side-in-a-github-readme-md
-->
