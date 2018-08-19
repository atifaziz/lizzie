﻿/*
 * Copyright (c) 2018 Thomas Hansen - thomas@gaiasoul.com
 *
 * Licensed under the terms of the MIT license, see the enclosed LICENSE
 * file for details.
 */

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using lizzie.exceptions;

namespace lizzie
{
    public class Tokenizer
    {
        readonly ITokenizer _tokenizer;

        public Tokenizer(ITokenizer tokenizer)
        {
            // Not passing in a tokenizer is a logical runtime error!
            _tokenizer = tokenizer ?? throw new NullReferenceException(nameof(tokenizer));
        }

        public IEnumerable<string> Tokenize(Stream stream)
        {
            // Notice! We do NOT take ownership over stream!
            StreamReader reader = new StreamReader(stream);
            while (true) {
                var token = _tokenizer.Next(reader);
                if (token == null)
                    break;
                yield return token;
            }
            yield break;
        }

        public IEnumerable<string> Tokenize(IEnumerable<Stream> streams)
        {
            foreach (var ixStream in streams) {
                foreach (var ixToken in Tokenize(ixStream)) {
                    yield return ixToken;
                }
            }
        }

        public IEnumerable<string> Tokenize(string code)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(code))) {
                foreach (var ix in Tokenize(stream)) {
                    yield return ix;
                }
            }
        }

        public IEnumerable<string> Tokenize(IEnumerable<string> code)
        {
            foreach (var ixCode in code) {
                foreach (var ixToken in Tokenize(ixCode)) {
                    yield return ixToken;
                }
            }
        }

        public static bool EatSpace(StreamReader reader)
        {
            var retVal = false;
            while (!reader.EndOfStream) {
                var ch = (char)reader.Peek();
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n') {
                    reader.Read();
                    retVal = true;
                } else {
                    break;
                }
            }
            return retVal;
        }

        public static bool EatLine(StreamReader reader)
        {
            var retVal = false;
            while(!reader.EndOfStream) {
                var ch = (char)reader.Read();
                if (ch == '\r' || ch == '\n') {
                    if (!reader.EndOfStream) {
                        ch = (char)reader.Peek();
                        if (ch == '\r' || ch == '\n')
                            reader.Read();
                    }
                    break;
                } else {
                    retVal = true;
                }
            }
            return retVal;
        }

        public static void EatUntil(StreamReader reader, string sequence)
        {
            if (string.IsNullOrEmpty(sequence))
                throw new LizzieTokenizerException("Can't read until empty sequence is found");
            var first = sequence[0];
            var buffer = "";
            while (true) {
                var ix = reader.Read();
                if (ix == -1)
                    return;
                if (buffer != "") {

                    // Seen first character.
                    buffer += (char)ix;
                    if (buffer == sequence) {
                        return; // Found sequence
                    } else if (buffer.Length == sequence.Length) {
                        buffer = ""; // Throwing away matches found so far, starting over again.
                    }
                } else {
                    if (first == (char)ix) {

                        // Beginning of sequence.
                        buffer += (char)ix;
                        if (buffer == sequence) {
                            return; // Sequence found
                        } else if (buffer.Length == sequence.Length) {
                            buffer = ""; // Throwing away matches found so far, starting over again.
                            if (first == (char)ix) {
                                buffer += (char)ix; // This is the opening token for sequence.
                            }
                        }
                    }
                }
            }
        }

        public static string ReadString (StreamReader reader, char stop = '"')
        {
            var builder = new StringBuilder ();
            for (var c = reader.Read (); c != -1; c = reader.Read ()) {
                switch (c) {
                    case '\\':
                        builder.Append (GetEscapedCharacter (reader, stop));
                        break;
                    case '\n':
                    case '\r':
                        throw new LizzieTokenizerException (string.Format ("String literal contains CR or LF characters."));
                    default:
                        if (c == stop)
                            return builder.ToString();
                        builder.Append ((char)c);
                        break;
                }
            }
            throw new ApplicationException (string.Format ("Syntax error, string literal not closed before end of input near '{0}'", builder));
        }

        /*
         * Returns escape character.
         */
        static string GetEscapedCharacter (StreamReader reader, char stop)
        {
            var ch = reader.Read();
            if (ch == -1)
                throw new LizzieTokenizerException("EOF found before string literal was closed");
            switch ((char)ch) {
                case '\\':
                    return "\\";
                case 'a':
                    return "\a";
                case 'b':
                    return "\b";
                case 'f':
                    return "\f";
                case 't':
                    return "\t";
                case 'v':
                    return "\v";
                case 'n':
                    return "\n";
                case 'r':
                    return "\r";
                case 'x':
                    return HexCharacter (reader);
                default:
                    if (ch == stop)
                        return stop.ToString();
                    throw new LizzieTokenizerException ("Invalid escape sequence found in string literal");
            }
        }

        /*
         * Returns hex encoded character.
         */
        static string HexCharacter(StreamReader reader)
        {
            var hexNumberString = "";
            for (var idxNo = 0; idxNo < 4; idxNo++)
                hexNumberString += (char)reader.Read();
            var integerNo = Convert.ToInt32(hexNumberString, 16);
            return Encoding.UTF8.GetString(BitConverter.GetBytes(integerNo).Reverse().ToArray());
        }
    }
}
