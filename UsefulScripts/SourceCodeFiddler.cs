/*************************************************************************
 * SOURCECODEFIDDLER (v1.2)
 * by Reev the Chameleon
 * 1 Jul 2
**************************************************************************
Usage:
Allow you to open a script source file through this class instance and
move around and manipulate code content while keeping both block comments
and line comments intact.

Update v1.1: Add support for variadic arguments in RemoveUnitl
Update v1.2: Add functions to stash and destash position, and add ReadUntil function
*/

using System.IO;
using System;

namespace Chameleon{

public class SourceCodeFiddler{
	public enum eCodeState{
		code,
		blockComment,
		lineComment,
	}
	private struct Position{
		public int line;
		public int index;
		public eCodeState codeState;
	}

	private string[] aLine;
	private int line;
	private int index; //index in that line
	private char lastChar;

	private bool bStashed;
	private Position savedPosition;

	public char ThisChar{ get{return aLine[line][index];} }
	public eCodeState CodeState{get; private set;}
	public bool readFile(string filePath){
		try{
			aLine = File.ReadAllLines(filePath);
			reset();
			return true;
		}catch(IOException){
			aLine = null;
			return false;
		}
	}
	public void writeFile(string filePath){
		File.WriteAllLines(filePath,aLine);
	}
	public void reset(){
		index = 0;
		line = 0;
		while(line<aLine.Length && aLine[line]=="")
			++line;
		lastChar = '\0';
		CodeState = eCodeState.code;
	}
	public bool nextChar(){ //moves to next char
		char c = aLine[line][index];
		switch(c){
			case '/':
				if(lastChar=='/' && CodeState!=eCodeState.blockComment)
					CodeState = eCodeState.lineComment;
				else if(lastChar=='*' && CodeState==eCodeState.blockComment){
					CodeState = eCodeState.code;
					lastChar = '\0';
					goto LMove;
					// block something like /* *// to be interpreted as line comment
				}
				break;
			case '*':
				if(lastChar=='/' && CodeState!=eCodeState.lineComment){
					CodeState = eCodeState.blockComment;
					lastChar = '\0';
					goto LMove; //block /*/*
				}
				break;
		}
		lastChar = c;
	LMove:
		if(++index >= aLine[line].Length){
			index = 0;
			while(true){
				if(++line >= aLine.Length)
					return false;
				if(aLine[line] != "")
					break;
			}
			if(CodeState == eCodeState.lineComment)
				CodeState = eCodeState.code;
		}
		return true;
	}
	private bool skipLineComment(){
		if(CodeState != eCodeState.lineComment)
			return true;
		index = 0;
		while(true){
			if(++line >= aLine.Length)
				return false;
			if(aLine[line] != "")
				break;
		}
		lastChar = '\0';
		CodeState = eCodeState.code;
		return true;
	}
	public bool moveToLine(int in_line){
		--in_line;
		if(in_line<0 || in_line>=aLine.Length)
			return false;
		reset();
		while(line < in_line){
			nextChar();
			skipLineComment();
		}
		return true;
	}
	public bool moveToChar(char c){ //moveToLine first!
		while(ThisChar!=c || CodeState!=eCodeState.code){
			if(CodeState == eCodeState.lineComment){
				if(!skipLineComment())
					return false;
			}
			else if(!nextChar())
				return false;
		}
		return true;
	}
	public string readUntil(params char[] aEndChar){
		string token = "";
		int indexStart = index;
		eCodeState prevCodeState = CodeState;
		while(Array.IndexOf(aEndChar,ThisChar)<=-1 || CodeState!=eCodeState.code){
			if(!nextChar())
				return token + aLine[line].Substring(indexStart);
			if(index==0){
				if(prevCodeState==eCodeState.code)
					token += aLine[line-1].Substring(indexStart);
				return token; //newline is whitespace, so token has ended
			}
			else if(CodeState == eCodeState.lineComment){
				token += aLine[line].Substring(indexStart,index-indexStart-2);
				return token;
			}
			else if(prevCodeState != CodeState){
				if(prevCodeState==eCodeState.code) //CodeState must be block comment
					token += aLine[line].Substring(indexStart,index-indexStart-2);
				else if(prevCodeState==eCodeState.blockComment)
					indexStart = index;
			}
		}
		token += aLine[line].Substring(indexStart,index-indexStart);
		return token;
	}
	public bool moveToNextToken(){
		int currentLine = line;
		while((!char.IsWhiteSpace(ThisChar) && ThisChar!=',')
			|| CodeState!=eCodeState.code)
		{
			if(CodeState == eCodeState.lineComment){
				if(!skipLineComment())
					return false;
				break;
			}
			if(line != currentLine)
				break; //new line is whitespace
			else if(!nextChar())
				return false;
		}
		while(!isTokenChar(ThisChar,false) || CodeState!=eCodeState.code){
			if(!nextChar())
				return false;
			if(CodeState == eCodeState.lineComment){
				if(!skipLineComment())
					return false;
				break;
			}
		}
		return true;
	}
	public bool moveToToken(string s){
		if(s.Length == 0)
			return false;
		while(true){
			if(!moveToNextToken())
				return false;
			int matchCount = 0;
			while(aLine[line][index+matchCount]==s[matchCount++]){
				if(matchCount==s.Length){
					if(index+matchCount>=aLine[line].Length||!char.IsLetterOrDigit(aLine[line][index+matchCount]))
						return true;
					break;
				}
				else if(index+matchCount >= aLine[line].Length)
					break;
			}
		}
	}
	public bool moveToEndToken(){
		while(isTokenChar(ThisChar) && CodeState==eCodeState.code)
			if(!nextChar())
				return false;
		return true;
	}
	public bool moveToCloseBrace(){
		int braceLevel = 0;
		while(true){
			if(!nextChar())
				return false;
			if(CodeState==eCodeState.code){
				if(ThisChar == '{')
					++braceLevel;
				else if(ThisChar == '}'){
					--braceLevel;
					if(braceLevel <= 0)
						return true;
				}
			}
		}
	}
	public void insert(string s,bool bAfter=false){ //NO check
		aLine[line] = aLine[line].Insert(index+(bAfter?1:0),s);
	}
	public void removeUntil(params char[] aEndChar){ //remove range [currentChar,endChar)
		int savedIndex = index;
		int savedLine = line;
		eCodeState savedCodeState = CodeState;

		int indexStart = index;
		eCodeState prevCodeState = CodeState;
		string sLine = aLine[line].Substring(0,indexStart);
		while(Array.IndexOf(aEndChar,ThisChar)<=-1 || CodeState!=eCodeState.code){
			if(!nextChar())
				break;
			if(index==0){
				if(prevCodeState == eCodeState.blockComment)
					aLine[line-1] = sLine + aLine[line-1].Substring(indexStart);
				else
					aLine[line-1] = sLine;
				while(char.IsWhiteSpace(ThisChar))
					nextChar(); //skip leading whitespace
				sLine = aLine[line].Substring(0,index);
				indexStart = index;
			}
			else if(CodeState == eCodeState.lineComment){
				aLine[line] = sLine + aLine[line].Substring(index-2);
				if(!skipLineComment())
					break;
				while(char.IsWhiteSpace(ThisChar))
					nextChar(); //skip leading whitespace
				sLine = aLine[line].Substring(0,index);
				indexStart = index;
			}
			else if(prevCodeState != CodeState){
				if(prevCodeState==eCodeState.code){ //CodeState must be block comment
					/* Triggered block comment */
					indexStart = index-2;
				}
				else if(prevCodeState==eCodeState.blockComment)
					sLine += aLine[line].Substring(indexStart,index-indexStart);
			}
			prevCodeState = CodeState;
		}
		aLine[line] = sLine + aLine[line].Substring(index);
		
		index = savedIndex;
		line = savedLine;
		CodeState = savedCodeState;
	}
	public void removeUntilNextToken(){
		int savedIndex = index;
		int savedLine = line;
		eCodeState savedCodeState = CodeState;

		int indexStart = index;
		eCodeState prevCodeState = CodeState;
		bool bEndThisToken = char.IsWhiteSpace(ThisChar);
		string sLine = aLine[line].Substring(0,indexStart);
		while((!bEndThisToken && !char.IsWhiteSpace(ThisChar)) ||
			(bEndThisToken && !isTokenChar(ThisChar,false)) ||
			CodeState!=eCodeState.code)
		{
			if(!nextChar())
				break;
			if(char.IsWhiteSpace(ThisChar))
				bEndThisToken = true;
			if(index==0){
				if(prevCodeState == eCodeState.blockComment)
					aLine[line-1] = sLine + aLine[line-1].Substring(indexStart);
				else
					aLine[line-1] = sLine;
				while(char.IsWhiteSpace(ThisChar))
					nextChar(); //keep leading whitespace
				sLine = aLine[line].Substring(0,index);
				indexStart = index;
			}
			else if(CodeState == eCodeState.lineComment){
				aLine[line] = sLine + aLine[line].Substring(index-2);
				if(!skipLineComment())
					break;
				while(char.IsWhiteSpace(ThisChar))
					nextChar(); //keep leading whitespace
				sLine = aLine[line].Substring(0,index);
				indexStart = index;
			}
			else if(prevCodeState != CodeState){
				if(prevCodeState==eCodeState.code){ //CodeState must be block comment
					/* Triggered block comment */
					indexStart = index-2;
				}
				else if(prevCodeState==eCodeState.blockComment)
					sLine += aLine[line].Substring(indexStart,index-indexStart);
			}
			prevCodeState = CodeState;
		}
		aLine[line] = sLine + aLine[line].Substring(index);
		
		index = savedIndex;
		line = savedLine;
		CodeState = savedCodeState;
	}
	public string nextToken(){
		if(!moveToNextToken())
			return null;
		string token = "";
		int indexStart = index;
		eCodeState prevCodeState = CodeState;
		while(isTokenChar(ThisChar) && CodeState==eCodeState.code){
			if(!nextChar())
				return token + aLine[line].Substring(indexStart);
			if(index==0){
				if(prevCodeState==eCodeState.code)
					token += aLine[line-1].Substring(indexStart);
				return token; //newline is whitespace, so token has ended
			}
			else if(CodeState == eCodeState.lineComment){
				token += aLine[line].Substring(indexStart,index-indexStart-2);
				return token;
			}
			else if(prevCodeState != CodeState){
				if(prevCodeState==eCodeState.code) //CodeState must be block comment
					token += aLine[line].Substring(indexStart,index-indexStart-2);
				else if(prevCodeState==eCodeState.blockComment)
					indexStart = index;
			}
		}
		token += aLine[line].Substring(indexStart,index-indexStart);
		return token;
	}
	public bool moveToNextCodeChar(){
		do{
			if(!nextChar())
				return false;
		}while(char.IsWhiteSpace(ThisChar) || CodeState!=eCodeState.code);
		return true;
	}
	private bool isTokenChar(char c,bool bDigit=true){
		return
			(bDigit ? char.IsLetterOrDigit(c) : char.IsLetter(c)) ||
			c == '_' ||
			c == '[' ||
			c == ']' ||
			c == '<' ||
			c == '>' ||
			c == '.'
		;
	}
	public void stashPosition(){
		bStashed = true;
		savedPosition.line = line;
		savedPosition.index = index;
		savedPosition.codeState = CodeState;
	}
	public void destashPosition(){
		if(bStashed){
			line = savedPosition.line;
			index = savedPosition.index;
			CodeState = savedPosition.codeState;
		}
	}
}

} //end namespace Chameleon
