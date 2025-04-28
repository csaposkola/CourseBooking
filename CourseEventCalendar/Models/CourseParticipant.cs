/*
' Copyright (c) 2025 Csaposkola
'  All rights reserved.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
' DEALINGS IN THE SOFTWARE.
'
*/

using DotNetNuke.ComponentModel.DataAnnotations;
using System;

namespace CourseEventCalendar.CourseEventCalendar.Models
{
    [TableName("CourseEventCalendar_CourseParticipants")]
    [PrimaryKey(nameof(ParticipantID), AutoIncrement = true)]
    [Scope("ModuleId")]
    public class CourseParticipant
    {
        public int ParticipantID { get; set; }

        public int EventID { get; set; }

        public int CreatedByUserID { get; set; }

        public DateTime CreatedOnDate { get; set; }

        public string ParticipantName { get; set; }

        public string Role { get; set; }

        public string CertificateNumber { get; set; }
    }
}